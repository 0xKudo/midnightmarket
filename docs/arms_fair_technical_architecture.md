# The Arms Fair — Technical Architecture Document
### Version 0.2 | Engineering Specification | Unity Edition

---

## Changelog from v0.1
- Engine changed from Godot 4 to Unity 2023 LTS (URP)
- Server changed from Node.js + TypeScript to ASP.NET Core 8 + SignalR
- Database ORM changed from Prisma to Entity Framework Core
- GeoJSON pipeline updated for Unity's C# ecosystem (NetTopologySuite)
- Globe rendering updated for Unity URP shaders and sphere mesh approach
- Steam integration updated from GodotSteam to Steamworks.NET
- Mobile platform targets added (iOS + Android future roadmap)
- All Godot-specific code examples replaced with Unity C#

---

## 1. System Overview

The Arms Fair runs as a client-server multiplayer application. The Unity client handles all rendering, UI, and player input across all platforms. An ASP.NET Core server manages all authoritative game state, phase logic, simulation calculations, and real-time communication via SignalR. A PostgreSQL database persists game rooms, player history, and completed game records.

The same server handles all client platforms — browser (WebGL), Steam desktop, and future mobile builds. All platform differences are handled on the client side. The server is fully platform-agnostic.

```
┌───────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                            │
│  Unity WebGL        Unity Desktop       Unity Mobile (future) │
│  Browser/itch.io    Steam (Win/Mac/Lin) iOS + Android         │
│  Steamworks: NO     Steamworks: YES     Steamworks: NO        │
└────────────────────────┬──────────────────────────────────────┘
                         │ SignalR WebSocket (wss://)
┌────────────────────────▼──────────────────────────────────────┐
│                      GAME SERVER                               │
│  ASP.NET Core 8 + SignalR                                     │
│  GameHub — all real-time WebSocket communication              │
│  REST API — lobby creation, auth, seed data                   │
│  Simulation engine — track math, spread, events               │
│  Phase orchestrator — timer management, phase transitions     │
└──────┬────────────────────────────────┬────────────────────────┘
       │                                │
┌──────▼──────────┐          ┌──────────▼──────────┐
│   PostgreSQL 16  │          │   External APIs      │
│   Game state     │          │   ACLED (seed only)  │
│   Player records │          │   Global Peace Index │
│   Game history   │          │   NewsAPI (ticker)   │
└──────────────────┘          └─────────────────────┘
       │
┌──────▼──────────┐
│   Redis 7        │
│   Active room    │
│   state cache    │
│   Sealed actions │
└──────────────────┘
```

---

## 2. Technology Stack

### 2.1 Client
| Component | Technology | Notes |
|---|---|---|
| Engine | Unity 2023 LTS | Long-term support — stable for multi-year project |
| Render pipeline | Universal Render Pipeline (URP) | Supports all target platforms including mobile and WebGL |
| Language | C# 12 | Shared with server via ArmsFair.Shared class library |
| 2D flat map | Unity UI Toolkit + Mesh generation | GeoJSON tessellated at load time into Mesh objects |
| 3D globe | Unity sphere mesh + URP custom shaders | Country overlay shader, atmosphere Fresnel, arc lines |
| View toggle | Camera switching + SceneManager.LoadSceneAdditive | Flat and globe scenes loaded simultaneously, cameras toggled |
| UI system | Unity UI Toolkit (USS/UXML) | Procurement screens, panels, chat, debrief |
| Networking | Microsoft.AspNetCore.SignalR.Client | Official SignalR client for Unity — NuGet package |
| Steam | Steamworks.NET | Desktop build only — conditional compile via #if UNITY_STANDALONE |
| Map data | Natural Earth GeoJSON 1:50m | Bundled as StreamingAssets — not fetched at runtime |
| Shader language | HLSL (URP ShaderGraph or hand-written) | Country heat overlay, atmosphere, arc lines |
| Mobile (future) | Unity iOS + Android build modules | Same codebase, platform-specific input and UI scaling |

### 2.2 Server
| Component | Technology | Notes |
|---|---|---|
| Framework | ASP.NET Core 8 | Cross-platform, high performance, runs on Linux |
| Language | C# 12 | Shared models with Unity client via ArmsFair.Shared |
| Real-time | SignalR (Microsoft.AspNetCore.SignalR) | Manages WebSocket connections, groups, reconnection |
| REST API | ASP.NET Core minimal APIs | Lobby creation, auth endpoints, health check |
| Database ORM | Entity Framework Core 8 + Npgsql | Type-safe DB access, code-first migrations |
| Cache client | StackExchange.Redis | Fast active game state reads/writes |
| Geometry | NetTopologySuite | GeoJSON parsing, adjacency graph, point-in-polygon |
| Process manager | systemd or Docker | Production process management |
| Validation | FluentValidation | Runtime validation on all incoming SignalR messages |

### 2.3 Shared Library
| Component | Technology | Notes |
|---|---|---|
| Project type | .NET 8 Class Library | Referenced by both Unity client and ASP.NET server |
| Contents | All data models, enums, Balance constants, message types | Defined once — used on both sides |
| Serialization | System.Text.Json | Built-in, fast, no extra dependency |
| Key benefit | Type safety across the wire | Changing a model field breaks both sides at compile time |

### 2.4 Database
| Component | Technology | Notes |
|---|---|---|
| Primary DB | PostgreSQL 16 | Game state, player records, completed games |
| Session cache | Redis 7 | Active room state — fast read/write during live games |
| Hosting | Railway or Render (indie) / AWS RDS (scale) | Start cheap, migrate as needed |

### 2.5 Infrastructure
| Component | Technology | Notes |
|---|---|---|
| Server hosting | Railway.app or Fly.io | Docker container, WebSocket support, cheap at indie scale |
| Static assets | Cloudflare R2 | GeoJSON, game assets for WebGL build |
| WebGL host | Cloudflare Pages or itch.io | Unity WebGL build upload |
| Steam | Steamworks SDK + SteamCMD | Steam Direct, $100 app fee |
| CI/CD | GitHub Actions | Automated builds for all export targets |

---

## 3. Solution Structure

The project uses a single .NET solution with three projects. Unity references the Shared library via a local NuGet package or direct DLL reference copied into Assets/Plugins/.

```
ArmsFair/                              # solution root
├── ArmsFair.sln
├── CLAUDE.md                          # Claude Code context file
├── docs/                              # all spec documents
│   ├── arms_fair_game_spec.md
│   ├── arms_fair_technical_architecture.md
│   └── arms_fair_balance.md
│
├── ArmsFair.Shared/                   # .NET 8 class library
│   ├── ArmsFair.Shared.csproj
│   ├── Balance.cs                     # all tunable constants
│   ├── Enums/
│   │   ├── SaleType.cs
│   │   ├── WeaponCategory.cs
│   │   ├── GamePhase.cs
│   │   └── CountryStage.cs
│   └── Models/
│       ├── GameState.cs
│       ├── PlayerAction.cs
│       ├── WorldTracks.cs
│       ├── CountryState.cs
│       └── Messages/                  # every SignalR message type
│           ├── ServerMessages.cs      # server → client
│           └── ClientMessages.cs      # client → server
│
├── ArmsFair.Server/                   # ASP.NET Core 8
│   ├── ArmsFair.Server.csproj
│   ├── Program.cs
│   ├── Hubs/
│   │   └── GameHub.cs                 # SignalR hub
│   ├── Simulation/
│   │   ├── TrackEngine.cs
│   │   ├── SpreadEngine.cs
│   │   ├── BlowbackEngine.cs
│   │   ├── CoupEngine.cs
│   │   └── EndingChecker.cs
│   ├── Data/
│   │   ├── AppDbContext.cs            # Entity Framework DbContext
│   │   └── Repositories/
│   └── Services/
│       ├── PhaseOrchestrator.cs
│       ├── SeedService.cs             # ACLED + GPI fetch
│       └── TickerService.cs           # news headline generation
│
└── ArmsFair.Unity/                    # Unity 2023 LTS project
    ├── Assets/
    │   ├── Plugins/
    │   │   └── ArmsFair.Shared.dll    # copied from Shared build output
    │   ├── StreamingAssets/
    │   │   └── GeoData/
    │   │       ├── countries.json     # preprocessed Natural Earth GeoJSON
    │   │       └── adjacency.json     # pre-computed border adjacency graph
    │   ├── Scripts/
    │   │   ├── Map/
    │   │   │   ├── MapLoader.cs       # GeoJSON → Unity Mesh
    │   │   │   ├── FlatMapRenderer.cs
    │   │   │   ├── GlobeRenderer.cs
    │   │   │   ├── ArcLineRenderer.cs # sale line animations
    │   │   │   └── CountrySelector.cs # click/tap detection
    │   │   ├── Network/
    │   │   │   ├── SignalRClient.cs   # hub connection management
    │   │   │   └── MessageHandler.cs  # incoming message dispatch
    │   │   ├── Game/
    │   │   │   ├── GameManager.cs     # singleton — all game state
    │   │   │   ├── PhaseManager.cs    # local phase UI/timer
    │   │   │   └── SteamManager.cs    # conditional Steam calls
    │   │   └── UI/
    │   │       ├── HUD.cs
    │   │       ├── ProcurementScreen.cs
    │   │       ├── NegotiationPanel.cs
    │   │       ├── RevealAnimator.cs
    │   │       ├── DebriefScreen.cs
    │   │       └── Leaderboard.cs
    │   ├── Scenes/
    │   │   ├── Main.unity             # persistent scene — UI and managers
    │   │   ├── MapFlat.unity          # 2D flat map scene (additive)
    │   │   └── MapGlobe.unity         # 3D globe scene (additive)
    │   ├── Shaders/
    │   │   ├── CountryOverlay.shader
    │   │   ├── GlobeAtmosphere.shader
    │   │   └── ArcLine.shader
    │   └── Materials/
    └── ProjectSettings/
```

---

## 4. Database Schema

Schema is unchanged from v0.1 — the database layer is fully server-side and not affected by the engine change. See original schema for all table definitions: players, games, game_players, world_state, country_state, player_actions, whistle_actions, treaties, treaty_signatories, game_events, supplier_relationships, reconstruction_contracts.

Entity Framework Core models mirror this schema exactly. Migrations are generated with:
```
dotnet ef migrations add InitialCreate --project ArmsFair.Server
dotnet ef database update --project ArmsFair.Server
```

---

## 5. SignalR Protocol

### 5.1 Connection

```csharp
// SignalRClient.cs — Unity client
using Microsoft.AspNetCore.SignalR.Client;

public class SignalRClient : MonoBehaviour
{
    private HubConnection _connection;

    public async Task ConnectAsync(string roomCode, string token)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"wss://api.armsfair.game/gamehub?room={roomCode}&token={token}")
            .WithAutomaticReconnect()
            .Build();

        // Register all incoming message handlers
        _connection.On<WorldUpdateMessage>("WorldUpdate", OnWorldUpdate);
        _connection.On<RevealMessage>("Reveal", OnReveal);
        _connection.On<ConsequencesMessage>("Consequences", OnConsequences);
        _connection.On<PhaseStartMessage>("PhaseStart", OnPhaseStart);
        _connection.On<ChatMessage>("ChatMsg", OnChatMessage);
        _connection.On<WhistleResultMessage>("WhistleResult", OnWhistleResult);
        _connection.On<GameEndingMessage>("GameEnding", OnGameEnding);
        _connection.On<StateSync>("StateSync", OnStateSync);

        await _connection.StartAsync();
    }
}
```

### 5.2 Server Hub

```csharp
// GameHub.cs — ASP.NET Core SignalR Hub
public class GameHub : Hub
{
    private readonly IPhaseOrchestrator _orchestrator;
    private readonly IGameRepository _games;

    // Client → Server: submit sealed action
    public async Task SubmitAction(SubmitActionMessage msg)
    {
        var validated = _validator.Validate(msg);
        if (!validated.IsValid) { await Clients.Caller.SendAsync("Error", validated.Errors); return; }

        var gameId = Context.Items["gameId"] as string;
        await _games.SealAction(gameId, Context.UserIdentifier, msg);
        await _orchestrator.CheckAllSubmitted(gameId);
    }

    // Client → Server: send chat message
    public async Task SendChat(ChatMessage msg)
    {
        var gameId = Context.Items["gameId"] as string;
        var player = Context.Items["player"] as GamePlayer;

        if (msg.RecipientId != null)
        {
            // Private DM — send only to target
            await Clients.User(msg.RecipientId).SendAsync("ChatMsg",
                new ChatMessage { SenderId = player.Id, Text = msg.Text, IsPrivate = true });
        }
        else
        {
            // Global — broadcast to room
            await Clients.Group(gameId).SendAsync("ChatMsg",
                new ChatMessage { SenderId = player.Id, Text = msg.Text, IsPrivate = false });
        }
    }

    // Client → Server: propose treaty
    public async Task ProposeTreaty(ProposeTreatyMessage msg) { /* ... */ }

    // Client → Server: execute whistle
    public async Task Whistle(WhistleMessage msg) { /* ... */ }

    // Client → Server: vote ceasefire
    public async Task VoteCeasefire(bool vote) { /* ... */ }

    // Client → Server: fund coup
    public async Task FundCoup(FundCoupMessage msg) { /* ... */ }

    // Client → Server: manufacture demand
    public async Task ManufactureDemand(ManufactureDemandMessage msg) { /* ... */ }
}
```

### 5.3 Shared Message Types

```csharp
// ArmsFair.Shared/Models/Messages/ServerMessages.cs
// Used by BOTH Unity client and ASP.NET server — defined once

public record PhaseStartMessage(
    GamePhase Phase,
    int Round,
    long EndsAt          // unix timestamp ms — client renders countdown
);

public record WorldUpdateMessage(
    TrackDeltas TrackDeltas,
    WorldTracks NewTracks,
    List<SpreadEvent> SpreadEvents,
    List<CountryChange> CountryChanges,
    List<GameEvent> Events
);

public record RevealMessage(
    List<RevealedAction> Actions,
    List<ArcAnimation> Animations    // sequenced animation data for Unity
);

public record ConsequencesMessage(
    List<ProfitUpdate> ProfitUpdates,
    List<ReputationUpdate> ReputationUpdates,
    List<SharePriceUpdate> SharePriceUpdates,
    List<BlowbackEvent> BlowbackEvents,
    List<HumanCostEvent> HumanCostEvents,
    List<TreatyResolution> TreatyResolutions,
    WorldTracks NewTracks
);

public record WhistleResultMessage(
    int Level,
    string TargetName,
    string? WeaponCategory,          // Level 1: only weapon category revealed
    ProcurementRecord? Procurement,  // Level 2: full procurement revealed
    SealedAction? Action,            // Level 3: full sealed action revealed
    bool IsAidFraud                  // Level 3: was it an aid cover lie?
);

public record GameEndingMessage(
    string EndingType,
    string TriggerDescription,
    List<FinalScore>? FinalScores
);

public record StateSync(
    GameState FullState              // complete state on reconnect
);
```

---

## 6. Simulation Engine

Unchanged from v0.1 — pure C# functions, no Unity dependencies. The simulation engine lives entirely in ArmsFair.Server and references only ArmsFair.Shared. It has zero knowledge of Unity. This means it is fully unit-testable with xUnit without running the game.

All balance constants remain in `ArmsFair.Shared/Balance.cs` — single source of truth referenced by both server simulation and client display logic.

---

## 7. GeoJSON Map Pipeline

### 7.1 Preprocessing (build-time Python script)

Run once before building. Reduces Natural Earth's ~25MB GeoJSON to ~3MB optimized for Unity.

```python
# tools/preprocess_geojson.py
import json
from shapely.geometry import shape
from shapely.ops import unary_union

with open('ne_50m_admin_0_countries.geojson') as f:
    data = json.load(f)

output = {'countries': [], 'adjacency': {}}

for feature in data['features']:
    props = feature['properties']
    iso = props['ADM0_A3']
    geom = shape(feature['geometry'])
    simplified = geom.simplify(0.1, preserve_topology=True)

    output['countries'].append({
        'iso': iso,
        'name': props['NAME'],
        'geometry': simplified.__geo_interface__
    })

# Build adjacency graph — which countries share a border
isos = [f['properties']['ADM0_A3'] for f in data['features']]
shapes = {iso: shape(f['geometry']).buffer(0.05)  # small buffer catches near-borders
          for iso, f in zip(isos, data['features'])}

for iso_a, geom_a in shapes.items():
    output['adjacency'][iso_a] = [
        iso_b for iso_b, geom_b in shapes.items()
        if iso_a != iso_b and geom_a.intersects(geom_b)
    ]

# Write to Unity StreamingAssets
with open('../ArmsFair.Unity/Assets/StreamingAssets/GeoData/countries.json', 'w') as f:
    json.dump(output['countries'], f, separators=(',', ':'))

with open('../ArmsFair.Unity/Assets/StreamingAssets/GeoData/adjacency.json', 'w') as f:
    json.dump(output['adjacency'], f, separators=(',', ':'))

print(f"Processed {len(output['countries'])} countries")
```

### 7.2 Unity Flat Map — GeoJSON to Mesh

```csharp
// MapLoader.cs — loads GeoJSON and generates Unity Mesh objects per country
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;

public class MapLoader : MonoBehaviour
{
    [SerializeField] private Material countryMaterial;
    [SerializeField] private float mapWidth = 1920f;
    [SerializeField] private float mapHeight = 960f;

    private Dictionary<string, List<GameObject>> _countryObjects = new();

    public async void LoadMap()
    {
        var path = Path.Combine(Application.streamingAssetsPath, "GeoData/countries.json");

        // Use UnityWebRequest for WebGL compatibility
        using var request = UnityEngine.Networking.UnityWebRequest.Get(path);
        await request.SendWebRequest();

        var countries = JArray.Parse(request.downloadHandler.text);

        foreach (var country in countries)
        {
            var iso = country["iso"].ToString();
            var geometry = country["geometry"];
            var type = geometry["type"].ToString();

            var polygons = type == "MultiPolygon"
                ? geometry["coordinates"].ToObject<float[][][][]>()
                : new[] { geometry["coordinates"].ToObject<float[][][]>() };

            _countryObjects[iso] = new List<GameObject>();

            foreach (var polygon in polygons)
            {
                var go = CreateCountryMesh(iso, polygon[0]); // outer ring only
                _countryObjects[iso].Add(go);
            }
        }
    }

    private GameObject CreateCountryMesh(string iso, float[][] coords)
    {
        var go = new GameObject($"Country_{iso}");
        go.transform.SetParent(transform);

        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var collider = go.AddComponent<PolygonCollider2D>();

        // Project lat/lng to screen coordinates (Mercator)
        var vertices2D = new Vector2[coords.Length];
        for (int i = 0; i < coords.Length; i++)
        {
            vertices2D[i] = ProjectMercator(coords[i][0], coords[i][1]);
        }

        // Set collider for click detection
        collider.SetPath(0, vertices2D);

        // Triangulate for mesh rendering
        var triangulator = new Triangulator(vertices2D);
        var indices = triangulator.Triangulate();

        var vertices3D = System.Array.ConvertAll(vertices2D, v => new Vector3(v.x, v.y, 0));

        var mesh = new Mesh();
        mesh.vertices = vertices3D;
        mesh.triangles = indices;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshRenderer.material = new Material(countryMaterial);

        return go;
    }

    private Vector2 ProjectMercator(float lng, float lat)
    {
        float x = (lng + 180f) / 360f * mapWidth;
        float latRad = lat * Mathf.Deg2Rad;
        float mercN = Mathf.Log(Mathf.Tan(Mathf.PI / 4f + latRad / 2f));
        float y = mapHeight / 2f - (mercN / Mathf.PI) * mapHeight / 2f;
        return new Vector2(x, y);
    }

    public void SetCountryColor(string iso, Color color)
    {
        if (!_countryObjects.ContainsKey(iso)) return;
        foreach (var go in _countryObjects[iso])
            go.GetComponent<MeshRenderer>().material.color = color;
    }
}
```

### 7.3 Unity 3D Globe — Country Overlay Shader

The globe uses a UV-sphere mesh. A custom URP shader maps lat/lng coordinates to country colors based on a pre-baked country ID texture and a per-country tension array updated each round.

```hlsl
// CountryOverlay.shader (URP Unlit ShaderGraph-compatible HLSL)
Shader "ArmsFair/CountryOverlay"
{
    Properties
    {
        _CountryIDMap ("Country ID Map", 2D) = "black" {}
        _BorderMap ("Border Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CountryIDMap);   SAMPLER(sampler_CountryIDMap);
            TEXTURE2D(_BorderMap);      SAMPLER(sampler_BorderMap);

            // Updated every round from C# via MaterialPropertyBlock
            float _CountryTensions[250];

            // Stage colors (0=dormant → 5=failed)
            static float4 STAGE_COLORS[6] = {
                float4(0.05, 0.12, 0.05, 1),  // 0 dormant — dark green
                float4(0.05, 0.25, 0.25, 1),  // 1 simmering — teal
                float4(0.50, 0.35, 0.02, 1),  // 2 active — amber
                float4(0.65, 0.12, 0.05, 1),  // 3 hot war — red
                float4(0.80, 0.05, 0.05, 1),  // 4 crisis — bright red
                float4(0.20, 0.20, 0.20, 1)   // 5 failed — gray
            };

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(TransformObjectToWorld(IN.positionOS.xyz)));
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample country ID (R=low byte, G=high byte)
                float4 idSample = SAMPLE_TEXTURE2D(_CountryIDMap, sampler_CountryIDMap, IN.uv);
                int countryIdx = (int)(idSample.r * 255 + idSample.g * 255 * 256);

                float tension = _CountryTensions[clamp(countryIdx, 0, 249)];
                int stage = clamp((int)(tension / 20.0), 0, 5);

                float4 baseColor = STAGE_COLORS[stage];

                // Border glow
                float border = SAMPLE_TEXTURE2D(_BorderMap, sampler_BorderMap, IN.uv).r;
                float4 borderColor = lerp(baseColor, float4(0.6, 0.8, 1.0, 1.0), border * 0.6);

                // Atmosphere fresnel
                float fresnel = pow(1.0 - saturate(dot(IN.normalWS, IN.viewDirWS)), 3.0);
                float4 atmo = float4(0.1, 0.3, 0.8, 1.0) * fresnel * 0.4;

                // Emission pulse for hot zones
                float emission = (tension / 100.0) * 0.25;

                return borderColor + atmo + float4(baseColor.rgb * emission, 0);
            }
            ENDHLSL
        }
    }
}
```

```csharp
// GlobeRenderer.cs — updates shader each round
public class GlobeRenderer : MonoBehaviour
{
    [SerializeField] private Renderer globeRenderer;
    private MaterialPropertyBlock _props;
    private float[] _tensions = new float[250];

    void Start() => _props = new MaterialPropertyBlock();

    public void UpdateCountryTensions(Dictionary<string, float> tensionByIso)
    {
        // Map ISO codes to texture indices (pre-built lookup)
        foreach (var kvp in tensionByIso)
        {
            if (IsoToIndex.TryGetValue(kvp.Key, out int idx))
                _tensions[idx] = kvp.Value;
        }

        globeRenderer.GetPropertyBlock(_props);
        _props.SetFloatArray("_CountryTensions", _tensions);
        globeRenderer.SetPropertyBlock(_props);
    }
}
```

### 7.4 2D/3D View Toggle

```csharp
// GameManager.cs — persistent singleton, survives scene loads
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Camera flatMapCamera;
    [SerializeField] private Camera globeCamera;

    public enum MapView { Flat, Globe }
    public MapView CurrentView { get; private set; } = MapView.Flat;

    // All game state — survives scene toggles
    public WorldTracks Tracks { get; set; }
    public List<CountryState> Countries { get; set; }
    public List<GamePlayer> Players { get; set; }
    public GamePhase CurrentPhase { get; set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async void ToggleView()
    {
        if (CurrentView == MapView.Flat)
        {
            // Load globe additively if not already loaded
            if (!IsSceneLoaded("MapGlobe"))
                await SceneManager.LoadSceneAsync("MapGlobe", LoadSceneMode.Additive);

            flatMapCamera.enabled = false;
            globeCamera.enabled = true;
            CurrentView = MapView.Globe;
        }
        else
        {
            flatMapCamera.enabled = true;
            globeCamera.enabled = false;
            CurrentView = MapView.Flat;
        }
    }

    // Auto-switch to globe for reveal phase
    public async void OnPhaseStart(GamePhase phase)
    {
        CurrentPhase = phase;
        if (phase == GamePhase.Reveal && CurrentView == MapView.Flat)
        {
            ToggleView();
            // Switch back after reveal animation completes
            await Task.Delay(30_000);
            if (CurrentPhase == GamePhase.Consequences) ToggleView();
        }
    }

    private bool IsSceneLoaded(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == name) return true;
        return false;
    }
}
```

---

## 8. Data Pipeline — Seed and Ticker

### 8.1 World Seed on Game Launch (Server-side)

The server fetches ACLED and GPI data once when a game room is created. After round 1, no external data is fetched. The simulation runs autonomously from round 2 onward.

```csharp
// SeedService.cs
public class SeedService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public async Task<WorldSeed> FetchWorldSeedAsync()
    {
        var acledTask = FetchACLEDAsync();
        var gpiTask   = FetchGPIAsync();
        await Task.WhenAll(acledTask, gpiTask);

        return BuildCountryTensions(await acledTask, await gpiTask);
    }

    private async Task<List<ACLEDEvent>> FetchACLEDAsync()
    {
        var key   = _config["ACLED:ApiKey"];
        var email = _config["ACLED:Email"];
        var since = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-DD");

        var url = $"https://api.acleddata.com/acled/read" +
                  $"?key={key}&email={email}" +
                  $"&event_date={since}|{DateTime.UtcNow:yyyy-MM-dd}" +
                  $"&event_date_where=BETWEEN" +
                  $"&fields=country,fatalities,event_type,event_date&limit=5000";

        var response = await _http.GetFromJsonAsync<ACLEDResponse>(url);
        return response?.Data ?? new();
    }

    private async Task<List<GPIRecord>> FetchGPIAsync()
    {
        // GPI dataset hosted as static JSON on your own server
        // Download from visionofhumanity.org and host it yourself
        // (GPI doesn't have a public API — use the annual CSV export)
        var url = _config["GPI:DataUrl"];
        return await _http.GetFromJsonAsync<List<GPIRecord>>(url) ?? new();
    }

    private WorldSeed BuildCountryTensions(List<ACLEDEvent> acled, List<GPIRecord> gpi)
    {
        var tensions = new Dictionary<string, float>();

        // GPI baseline: scale 1.0-3.0 → 0-60 tension
        foreach (var r in gpi)
            tensions[r.Iso3] = (r.Score - 1f) / 2f * 60f;

        // ACLED fatality overlay: adds up to +40 tension
        var byCountry = acled.GroupBy(e => e.Country);
        foreach (var group in byCountry)
        {
            var totalFatalities = group.Sum(e => e.Fatalities);
            var bonus = Mathf.Min(40f, Mathf.Log(totalFatalities + 1) * 5f);
            var iso = CountryNameToIso(group.Key);
            if (iso != null && tensions.ContainsKey(iso))
                tensions[iso] = Mathf.Min(100f, tensions[iso] + bonus);
        }

        return new WorldSeed
        {
            Countries = tensions.Select(kvp => new CountrySeed
            {
                Iso         = kvp.Key,
                Tension     = (int)kvp.Value,
                Stage       = TensionToStage(kvp.Value),
                DemandType  = kvp.Value > 40 ? (kvp.Value > 70 ? "open" : "covert") : "none"
            }).ToList(),
            FetchedAt = DateTime.UtcNow
        };
    }

    private static int TensionToStage(float tension) =>
        tension >= 85 ? 4 :
        tension >= 65 ? 3 :
        tension >= 40 ? 2 :
        tension >= 20 ? 1 : 0;
}
```

### 8.2 News Ticker

Round 1 ticker uses real NewsAPI headlines filtered by active conflict country names. Round 2+ ticker uses procedural templates filled with game state data.

```csharp
// TickerService.cs
public class TickerService
{
    private readonly HttpClient _http;
    private readonly string _newsApiKey;

    // Round 1 — real headlines
    public async Task<List<string>> FetchRealHeadlinesAsync(List<string> countryNames)
    {
        var query = string.Join(" OR ", countryNames.Take(5));
        var url = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}" +
                  $"&sortBy=publishedAt&pageSize=20&apiKey={_newsApiKey}";

        var response = await _http.GetFromJsonAsync<NewsApiResponse>(url);
        return response?.Articles?.Select(a => a.Title).ToList() ?? new();
    }

    // Round 2+ — procedural
    private static readonly string[] TEMPLATES =
    {
        "{company} shipment to {country} under investigation by UN panel",
        "Conflict in {country} enters month {duration} — civilian toll passes {number}",
        "Arms embargo on {country} discussed at Security Council",
        "{country} government forces advance using {weapon_category}",
        "Red Cross suspended operations in {country} citing security deterioration",
        "Reconstruction tenders open in {country} — international firms invited to bid",
        "Market analysts: defense sector earnings up {percent}% — attributed to {region} instability",
        "{company} stock falls {points} points following {country} blowback reports",
        "Peace talks in {country} stall for {number}th consecutive round",
    };

    public string GenerateTicker(GameState state)
    {
        var template = TEMPLATES[Random.Shared.Next(TEMPLATES.Length)];
        return FillTemplate(template, state);
    }
}
```

---

## 9. Steam Integration (Unity)

Steam integration uses Steamworks.NET, wrapped in a conditional compile block so the same codebase builds cleanly for WebGL and mobile.

```csharp
// SteamManager.cs
using UnityEngine;
#if UNITY_STANDALONE && !UNITY_EDITOR_OSX
using Steamworks;
#endif

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }
    public bool IsSteamBuild { get; private set; }

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_STANDALONE && !UNITY_EDITOR_OSX
        try
        {
            if (!SteamAPI.Init())
                Debug.LogError("Steam init failed — make sure steam_appid.txt is present");
            else
                IsSteamBuild = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Steam exception: {e.Message}");
        }
#endif
    }

    void OnDestroy()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR_OSX
        if (IsSteamBuild) SteamAPI.Shutdown();
#endif
    }

    public void UnlockAchievement(string achievementId)
    {
#if UNITY_STANDALONE && !UNITY_EDITOR_OSX
        if (!IsSteamBuild) return;
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
#endif
    }

    public void OpenOverlayUrl(string url)
    {
#if UNITY_STANDALONE && !UNITY_EDITOR_OSX
        if (IsSteamBuild) { SteamFriends.ActivateGameOverlayToWebPage(url); return; }
#endif
        Application.OpenURL(url);
    }
}
```

### 9.1 Achievements (unchanged from v0.1)

| Achievement ID | Name | Trigger |
|---|---|---|
| `first_blood` | First Blood | Complete your first sale |
| `peacemaker` | Peacemaker | Win a Negotiated Peace ending |
| `war_profiteer` | War Profiteer | Earn $200M in a single game |
| `whistleblower` | Whistleblower | Execute 3 Level 3 exposes in one game |
| `failed_state` | Your Fault | Be sole seller into a country reaching Failed State |
| `total_war` | What Have You Done | Be in a game that reaches Total War |
| `reconstruction` | Nation Builder | Win 3 reconstruction contracts in one game |
| `gray_ghost` | Gray Ghost | Complete 5 covert sales without a single trace |
| `regime_change` | Regime Change | Successfully execute a coup |
| `debrief` | The Weight | Watch the full debrief after Total War |

---

## 10. Build and Deployment

### 10.1 Unity Build Targets

| Platform | Build Target | Notes |
|---|---|---|
| Windows (Steam) | PC, Mac & Linux Standalone | Includes Steamworks.NET — `UNITY_STANDALONE` define |
| macOS (Steam) | PC, Mac & Linux Standalone | Same build target, different platform selection |
| Linux (Steam) | PC, Mac & Linux Standalone | Steam Deck compatible |
| WebGL (browser) | WebGL | Excludes Steam — `UNITY_WEBGL` define |
| iOS (future) | iOS | Requires Mac build machine + Apple Developer account |
| Android (future) | Android | Keystore signing required for Play Store |

**Scripting Define Symbols** — set in Project Settings → Player per platform:
- Desktop builds: `UNITY_STANDALONE;STEAM_BUILD`
- WebGL builds: `UNITY_WEBGL`
- Mobile builds: `UNITY_MOBILE`

### 10.2 WebGL-Specific Considerations

```csharp
// Anywhere you need platform-specific behavior:
#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL: use JavaScript interop for file access
    Application.ExternalEval("...");
#else
    // Desktop/mobile: use normal C# file APIs
    File.ReadAllText(path);
#endif
```

WebGL builds require specific HTTP response headers on the host server for threading support:
```
# Cloudflare Pages _headers file
/*
  Cross-Origin-Embedder-Policy: require-corp
  Cross-Origin-Opener-Policy: same-origin
```

### 10.3 Server Deployment (Railway.app)

```dockerfile
# ArmsFair.Server/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ArmsFair.Server/ArmsFair.Server.csproj", "ArmsFair.Server/"]
COPY ["ArmsFair.Shared/ArmsFair.Shared.csproj", "ArmsFair.Shared/"]
RUN dotnet restore "ArmsFair.Server/ArmsFair.Server.csproj"
COPY . .
RUN dotnet build "ArmsFair.Server/ArmsFair.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ArmsFair.Server/ArmsFair.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ArmsFair.Server.dll"]
```

```yaml
# railway.toml
[build]
builder = "dockerfile"
dockerfilePath = "ArmsFair.Server/Dockerfile"

[deploy]
healthcheckPath = "/health"
restartPolicyType = "always"
```

Required environment variables (set in Railway dashboard):
```
ConnectionStrings__Default   — PostgreSQL connection string
Redis__ConnectionString      — Redis connection string
JWT__Secret                  — 256-bit random secret
ACLED__ApiKey                — ACLED API key
ACLED__Email                 — ACLED registered email
NewsAPI__ApiKey              — NewsAPI key
GPI__DataUrl                 — URL to your hosted GPI JSON file
ASPNETCORE_ENVIRONMENT       — Production
```

### 10.4 GitHub Actions CI/CD

```yaml
# .github/workflows/deploy-server.yml
name: Deploy Server

on:
  push:
    branches: [main]
    paths: ['ArmsFair.Server/**', 'ArmsFair.Shared/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Deploy to Railway
        run: railway up --service game-server
        env:
          RAILWAY_TOKEN: ${{ secrets.RAILWAY_TOKEN }}
```

Unity builds are handled locally using Unity's build pipeline or Unity Cloud Build — automated Unity builds in GitHub Actions require a Unity license on the CI runner, which has cost implications. For indie scale, building locally and uploading to Steam/itch.io manually is the practical approach.

---

## 11. Performance Targets

| Metric | Target | Notes |
|---|---|---|
| SignalR latency | < 100ms | Server in same region as player base |
| Phase transition | < 500ms | Timer expiry to all clients receiving PhaseStart |
| Reveal animation | 60fps desktop, 30fps WebGL | Globe arc animations |
| WebGL build size | < 80MB | Unity WebGL is larger than Godot — acceptable for strategy game |
| Mobile frame rate | 60fps iOS, 30fps Android minimum | URP is well-optimized for mobile |
| Concurrent rooms | 50+ | Single Railway instance — scale horizontally after |
| DB queries per round | < 20 | Most state in Redis during active games |
| Simulation engine | < 50ms | Full round calculation |

---

## 12. Open Technical Questions

1. **Country ID texture baking.** The 3D globe URP shader needs a pre-baked country ID texture mapping UV coordinates to country indices. Best approach: write a Python script using Pillow and Shapely to rasterize the GeoJSON polygons directly to a 4096×2048 PNG using equirectangular projection. Each country gets a unique color ID. Antialiasing must be disabled at borders — nearest-neighbor sampling only.

2. **SignalR client for Unity WebGL.** The official Microsoft.AspNetCore.SignalR.Client NuGet package works in Unity standalone builds but has WebGL compatibility issues due to threading. The recommended workaround is to use the community-maintained `HybridWebSocket` package for WebGL and the official SignalR client for standalone, swapped via conditional compile.

3. **Shared library in Unity.** Unity cannot directly reference a .NET solution project. The workflow is: build ArmsFair.Shared as a DLL, copy the output DLL and its .pdb into Assets/Plugins/. Add this as a build step in GitHub Actions. For local development, a post-build script in ArmsFair.Shared.csproj copies the DLL automatically.

4. **ACLED rate limits.** Cache the seed response server-side for 24 hours in Redis. All rooms created within that window use the cached seed. Only the first room creation per day triggers an actual ACLED API call.

5. **GPI data hosting.** The Global Peace Index does not have a public API. Download the annual CSV from visionofhumanity.org, convert to JSON, and host it on Cloudflare R2. Update once per year. The SeedService fetches from your hosted URL.

6. **Mobile input for country selection.** The flat map uses Physics2D.OverlapPoint for click detection on desktop. On mobile, replace with touch input via Input.GetTouch and convert screen position to world position via Camera.ScreenToWorldPoint. The globe uses Physics.Raycast against sphere collider on both platforms.

---

*End of Technical Architecture v0.2*
*Engine: Unity 2023 LTS | Server: ASP.NET Core 8 + SignalR*
*Next documents: UI/UX wireframe spec*
