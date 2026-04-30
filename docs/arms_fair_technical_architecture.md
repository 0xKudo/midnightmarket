# The Arms Fair — Technical Architecture Document
### Version 0.2 | Engineering Specification | Unity Edition

---

## Changelog

### v0.4 (current) — 2026-04-29
- **IMPLEMENTED:** PhaseOrchestrator — single authority for phase transitions, absorbed from GameHub.AdvancePhase and TickerService. Runs SpreadEngine on WorldUpdate, Reveal/Consequences logic on Reveal entry, EndingChecker after every advance.
- **IMPLEMENTED:** AuthService — JWT mint/validate (HS256, 7-day expiry), BCrypt password hashing (work factor 12), register/login/validateToken.
- **IMPLEMENTED:** PlayerEntity — persistent player account EF entity. players table with username (unique, 3-20 chars), email, password_hash, steam_id, home_nation_iso, created_at, last_login_at, is_banned.
- **IMPLEMENTED:** REST auth endpoints — POST /api/auth/register, POST /api/auth/login, GET /api/auth/me.
- **IMPLEMENTED:** adjacency.json copied to ArmsFair.Server/GeoData — server-side spread engine uses it.
- **NOT YET:** EF Core migrations not run. DB schema must be created before deployment.
- **NOT YET:** StatsService, ChatRepository, AccountRepository, VivoxTokenService — deferred.
- **NOT YET:** Consequences do not yet mutate PlayerProfile.Capital/Reputation in GameState (deltas computed, broadcast, but not applied back to state).

### v0.3
- Added Section 9: Authentication and Account System
- Added Section 10: Player Statistics schema and server logic
- Added Section 11: Text Chat — SignalR channels, persistence, moderation
- Added Section 12: Voice Chat — Unity Vivox / Unity Gaming Services integration
- Updated Section 2: Technology stack expanded for auth, voice, and UGS
- Updated Section 3: Solution structure expanded for new scripts and services
- Updated Section 4: Database schema expanded for accounts, stats, and chat history
- Updated Section 5: SignalR protocol expanded with all chat message types

### v0.2
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
| Text chat | SignalR — same hub connection as game | No separate connection needed — chat is a channel in GameHub |
| Voice chat | Unity Gaming Services — Vivox | Free for indie scale, first-party Unity voice SDK |
| Auth (browser) | JWT via REST — email/password or Google OAuth | Stored in PlayerPrefs, attached to SignalR connection |
| Auth (Steam) | Steamworks session ticket → server JWT exchange | Same JWT system after exchange — server is auth-agnostic |
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
| REST API | ASP.NET Core minimal APIs | Lobby, auth, account registration, health check |
| Auth tokens | JWT Bearer (System.IdentityModel.Tokens.Jwt) | Stateless — token attached to every SignalR connection |
| Password hashing | BCrypt.Net-Next | Never store plain passwords |
| Database ORM | Entity Framework Core 8 + Npgsql | Type-safe DB access, code-first migrations |
| Cache client | StackExchange.Redis | Fast active game state, chat rate limiting |
| Geometry | NetTopologySuite | GeoJSON parsing, adjacency graph, point-in-polygon |
| Voice | Vivox server-side token generation | Server mints Vivox access tokens — client uses them with UGS |
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

The project uses a single .NET solution with three projects. Unity references the Shared library via a compiled DLL copied into Assets/Plugins/.

```
ArmsFair/                              # solution root
├── ArmsFair.sln
├── CLAUDE.md
├── docs/
│   ├── arms_fair_game_spec.md
│   ├── arms_fair_technical_architecture.md
│   └── arms_fair_balance.md
│
├── ArmsFair.Shared/
│   ├── ArmsFair.Shared.csproj
│   ├── Balance.cs
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
│       ├── PlayerProfile.cs           # account + stats model
│       ├── PlayerStats.cs             # lifetime statistics record
│       └── Messages/
│           ├── ServerMessages.cs      # server → client
│           ├── ClientMessages.cs      # client → server
│           └── ChatMessages.cs        # all chat message types
│
├── ArmsFair.Server/
│   ├── ArmsFair.Server.csproj
│   ├── Program.cs
│   ├── Hubs/
│   │   └── GameHub.cs                 # SignalR hub — game + chat
│   ├── Simulation/
│   │   ├── TrackEngine.cs
│   │   ├── SpreadEngine.cs
│   │   ├── BlowbackEngine.cs
│   │   ├── CoupEngine.cs
│   │   └── EndingChecker.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Repositories/
│   │       ├── GameRepository.cs
│   │       ├── AccountRepository.cs   # player accounts + stats
│   │       └── ChatRepository.cs      # chat history persistence
│   └── Services/
│       ├── PhaseOrchestrator.cs
│       ├── SeedService.cs
│       ├── TickerService.cs
│       ├── AuthService.cs             # JWT mint/validate, bcrypt
│       ├── StatsService.cs            # lifetime stat updates
│       └── VivoxTokenService.cs       # mint Vivox access tokens
│
└── ArmsFair.Unity/
    ├── Assets/
    │   ├── Plugins/
    │   │   └── ArmsFair.Shared.dll
    │   ├── StreamingAssets/
    │   │   └── GeoData/
    │   │       ├── countries.json
    │   │       └── adjacency.json
    │   ├── Scripts/
    │   │   ├── Map/
    │   │   │   ├── MapLoader.cs
    │   │   │   ├── FlatMapRenderer.cs
    │   │   │   ├── GlobeRenderer.cs
    │   │   │   ├── ArcLineRenderer.cs
    │   │   │   └── CountrySelector.cs
    │   │   ├── Network/
    │   │   │   ├── SignalRClient.cs
    │   │   │   └── MessageHandler.cs
    │   │   ├── Game/
    │   │   │   ├── GameManager.cs
    │   │   │   ├── PhaseManager.cs
    │   │   │   └── SteamManager.cs
    │   │   ├── Auth/
    │   │   │   ├── AccountManager.cs  # login, register, session restore
    │   │   │   └── AuthApiClient.cs   # REST calls to /api/auth/*
    │   │   ├── Chat/
    │   │   │   ├── ChatManager.cs     # routes messages to correct panel
    │   │   │   ├── ChatPanel.cs       # UI controller
    │   │   │   └── ChatMessage.cs     # display component
    │   │   ├── Voice/
    │   │   │   ├── VoiceChatManager.cs  # Vivox connection + phase muting
    │   │   │   └── VoiceUI.cs           # mic indicators, PTT button
    │   │   └── UI/
    │   │       ├── HUD.cs
    │   │       ├── ProcurementScreen.cs
    │   │       ├── NegotiationPanel.cs
    │   │       ├── RevealAnimator.cs
    │   │       ├── DebriefScreen.cs
    │   │       ├── Leaderboard.cs
    │   │       ├── ProfileScreen.cs   # username, stats display
    │   │       └── LobbyScreen.cs     # room creation/join
    │   ├── Scenes/
    │   │   ├── Bootstrap.unity        # auth check before anything loads
    │   │   ├── Login.unity            # login / register screen
    │   │   ├── Lobby.unity            # room browser and creation
    │   │   ├── Main.unity             # persistent scene — managers
    │   │   ├── MapFlat.unity
    │   │   └── MapGlobe.unity
    │   ├── Shaders/
    │   │   ├── CountryOverlay.shader
    │   │   ├── GlobeAtmosphere.shader
    │   │   └── ArcLine.shader
    │   └── Materials/
    └── ProjectSettings/
```

---

## 4. Database Schema

### 4.1 Original Game Tables
All original tables remain unchanged: games, game_players, world_state, country_state, player_actions, whistle_actions, treaties, treaty_signatories, game_events, supplier_relationships, reconstruction_contracts. See v0.1 for full SQL definitions.

### 4.2 New Tables — Accounts, Stats, and Chat

```sql
-- Player accounts (expanded from v0.1 players table)
CREATE TABLE players (
  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  username          VARCHAR(20) UNIQUE NOT NULL,         -- 3-20 chars, unique globally
  username_changed_at TIMESTAMPTZ,                       -- enforce 30-day change cooldown
  email             VARCHAR(255) UNIQUE,                 -- null for Steam-only accounts
  password_hash     VARCHAR(255),                        -- bcrypt, null for Steam-only
  steam_id          VARCHAR(32) UNIQUE,                  -- null for email accounts
  home_nation_iso   VARCHAR(3) NOT NULL DEFAULT 'USA',   -- default flag/avatar
  created_at        TIMESTAMPTZ DEFAULT NOW(),
  last_login_at     TIMESTAMPTZ,
  is_banned         BOOLEAN DEFAULT FALSE
);

-- Player lifetime statistics (1:1 with players)
CREATE TABLE player_stats (
  player_id               UUID PRIMARY KEY REFERENCES players(id),
  games_played            INTEGER DEFAULT 0,
  games_won               INTEGER DEFAULT 0,
  wars_started            INTEGER DEFAULT 0,
  failed_states_caused    INTEGER DEFAULT 0,
  small_arms_sold         INTEGER DEFAULT 0,
  vehicles_sold           INTEGER DEFAULT 0,
  air_defense_sold        INTEGER DEFAULT 0,
  drones_sold             INTEGER DEFAULT 0,
  total_profit_earned     BIGINT DEFAULT 0,               -- $M lifetime
  total_civilian_cost     BIGINT DEFAULT 0,               -- lifetime civ cost contribution
  ceasefires_brokered     INTEGER DEFAULT 0,
  coups_funded            INTEGER DEFAULT 0,
  coups_succeeded         INTEGER DEFAULT 0,
  times_whistleblown      INTEGER DEFAULT 0,
  times_whistleblower     INTEGER DEFAULT 0,
  total_war_participations INTEGER DEFAULT 0,
  world_peace_achieved    INTEGER DEFAULT 0,
  company_collapses       INTEGER DEFAULT 0,
  reconstruction_wins     INTEGER DEFAULT 0,
  legacy_score_total      BIGINT DEFAULT 0,               -- cumulative legacy score
  updated_at              TIMESTAMPTZ DEFAULT NOW()
);

-- Chat messages (persisted for debrief and moderation)
CREATE TABLE chat_messages (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  game_id         UUID REFERENCES games(id),
  sender_id       UUID REFERENCES players(id),
  recipient_id    UUID REFERENCES players(id),           -- null = global channel
  message_text    VARCHAR(280) NOT NULL,
  is_private      BOOLEAN DEFAULT FALSE,
  is_system       BOOLEAN DEFAULT FALSE,                 -- system announcements
  sent_at         TIMESTAMPTZ DEFAULT NOW(),
  round_number    INTEGER,                               -- which round it was sent in
  phase           VARCHAR(16)                            -- which phase it was sent in
);
CREATE INDEX idx_chat_game ON chat_messages(game_id, sent_at);
CREATE INDEX idx_chat_private ON chat_messages(game_id, sender_id, recipient_id)
  WHERE is_private = TRUE;

-- Mute list (player mutes another player — game-scoped)
CREATE TABLE player_mutes (
  game_id         UUID REFERENCES games(id),
  muter_id        UUID REFERENCES players(id),
  muted_id        UUID REFERENCES players(id),
  created_at      TIMESTAMPTZ DEFAULT NOW(),
  PRIMARY KEY(game_id, muter_id, muted_id)
);

-- Chat rate limiting (tracks per-player message count)
-- Handled in Redis (not PostgreSQL) for performance:
-- Key: chat_rate:{game_id}:{player_id}  Value: message count  TTL: 10 seconds
-- If count >= 5: reject message, return rate limit error to client
```

Entity Framework Core models mirror this schema. Generate migrations with:
```
dotnet ef migrations add AddAccountsAndChat --project ArmsFair.Server
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
public record PhaseStartMessage(
    GamePhase Phase,
    int Round,
    long EndsAt,
    bool VoiceEnabled           // whether voice is active this phase
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
    List<ArcAnimation> Animations
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
    string? WeaponCategory,
    ProcurementRecord? Procurement,
    SealedAction? Action,
    bool IsAidFraud
);

public record GameEndingMessage(
    string EndingType,
    string TriggerDescription,
    List<FinalScore>? FinalScores
);

public record StateSync(GameState FullState);

// ArmsFair.Shared/Models/Messages/ChatMessages.cs
// Chat — server → client
public record ChatMessageReceived(
    string MessageId,
    string SenderId,
    string SenderUsername,
    string SenderColor,          // player's assigned color hex
    string Text,
    bool IsPrivate,
    bool IsSystem,
    string? RecipientId,         // null = global
    long SentAt,                 // unix ms
    int RoundNumber,
    string Phase
);

// Chat — client → server
public record SendChatMessage(
    string Text,
    string? RecipientId          // null = global, string = DM target player ID
);

// Mute — client → server
public record MutePlayerMessage(string TargetPlayerId);

// Voice token — server → client (sent at phase start when voice is active)
public record VivoxTokenMessage(
    string AccessToken,          // Vivox JWT minted by server
    string ChannelUri,           // Vivox channel to join
    bool PushToTalkRequired      // room setting
);

// Voice state change — server → client (broadcasts when phase changes voice state)
public record VoiceStateMessage(
    bool IsActive,
    string Reason                // "negotiation_phase" | "sealed_phase" | "reveal_delay" etc.
);

// Player speaking indicator — client → server → all clients
// Vivox handles this natively — no custom message needed
// Use Vivox's IParticipantUpdatedEvent instead
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

### 7.3 Unity 3D Globe — WPM Globe Edition Lite

The globe uses **World Political Map Globe Edition Lite** (WPMapGlobeEditionLite asset). WPM provides built-in country borders, labels, click detection, auto-rotation, and zoom. A custom bridge script (`WPMGlobeBridge.cs`) connects WPM events to game logic.

**Key WPM settings applied at runtime (WPMGlobeBridge.Start):**
- `mouseWheelSensitivity = 0.5f` — slowed from default for comfortable zoom
- `autoRotationSpeed` — cached and restored after idle (2s delay)
- `showCursor = false` — hides WPM's crosshair cursor lines

**Click detection:** Poll `_map.countryLastClicked` each Update(). WPM returns an index into `_map.countries[]`. Country name is resolved to ISO alpha-3 via a name→ISO dictionary loaded from `StreamingAssets/GeoData/countries.json`.

**Zoom clamping (WPMGlobeBridge.Update):**
- Zoom-in limit: `GetZoomLevel() < 0.3f` → clamp to 0.3
- Zoom-out limit: camera distance from globe center clamped to `1.75f` units

**Auto-rotation:** Paused while `mouseIsOver || GetMouseButton(0)`. Resumes after 2s idle.

**Input system:** `ProjectSettings activeInputHandler = 2` (Both old + new input systems) required so WPM's `OnMouseEnter`/`OnMouseExit` fire correctly alongside Unity's New Input System.

**WPMInternal.cs patch:** Scroll wheel is read unconditionally (not gated on `mouseIsOver`) so zoom works regardless of cursor position.

**Tension colors:** `UpdateCountryTensions(Dictionary<string, float>)` calls `ToggleCountrySurface()` per country with one of 6 stage colors (Stable → Failed).

### 7.3.1 Country Overlay Shader (legacy — superseded by WPM)

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

### 8.1 World Seed — All Game Modes

The `SeedService` handles all five game modes. Only Realistic mode makes external API calls. All other modes are self-contained and instantaneous.

```csharp
// SeedService.cs
public class SeedService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IRedisCache _cache;

    public async Task<WorldSeed> BuildSeedAsync(GameMode mode,
        CustomScenarioConfig? customConfig = null)
    {
        return mode switch
        {
            GameMode.Realistic   => await BuildRealisticSeedAsync(),
            GameMode.EqualWorld  => BuildEqualWorldSeed(),
            GameMode.BlankSlate  => BuildBlankSlateSeed(),
            GameMode.HotWorld    => BuildHotWorldSeed(),
            GameMode.Custom      => BuildCustomSeed(customConfig!),
            _ => throw new ArgumentException($"Unknown game mode: {mode}")
        };
    }

    // --- Mode 1: Realistic ---
    private async Task<WorldSeed> BuildRealisticSeedAsync()
    {
        // Check Redis cache first — valid for 24 hours
        const string cacheKey = "world_seed:realistic";
        var cached = await _cache.GetAsync<WorldSeed>(cacheKey);
        if (cached != null) return cached;

        var acledTask = FetchACLEDAsync();
        var gpiTask   = FetchGPIAsync();
        await Task.WhenAll(acledTask, gpiTask);

        var seed = BuildCountryTensions(await acledTask, await gpiTask);
        seed.StartingTracks = new WorldTracks
        {
            MarketHeat    = 30,
            CivilianCost  = 20,
            Stability     = 25,
            SanctionsRisk = 10,
            GeoTension    = 35
        };

        await _cache.SetAsync(cacheKey, seed, TimeSpan.FromHours(24));
        return seed;
    }

    // --- Mode 2: Equal World ---
    private WorldSeed BuildEqualWorldSeed()
    {
        return new WorldSeed
        {
            Mode = GameMode.EqualWorld,
            Countries = GetAllCountryISOs().Select(iso => new CountrySeed
            {
                Iso        = iso,
                Tension    = 25,
                Stage      = 1,          // Simmering
                DemandType = "covert"    // covert-only at start
            }).ToList(),
            StartingTracks = new WorldTracks
            {
                MarketHeat    = 20,
                CivilianCost  = 10,
                Stability     = 15,
                SanctionsRisk = 5,
                GeoTension    = 20
            }
        };
    }

    // --- Mode 3: Blank Slate ---
    private WorldSeed BuildBlankSlateSeed()
    {
        return new WorldSeed
        {
            Mode = GameMode.BlankSlate,
            Countries = GetAllCountryISOs().Select(iso => new CountrySeed
            {
                Iso        = iso,
                Tension    = 5,
                Stage      = 0,          // Dormant
                DemandType = "none"
            }).ToList(),
            StartingTracks = new WorldTracks
            {
                MarketHeat    = 10,
                CivilianCost  = 5,
                Stability     = 10,
                SanctionsRisk = 0,
                GeoTension    = 10
            }
        };
    }

    // --- Mode 4: Hot World ---
    private WorldSeed BuildHotWorldSeed()
    {
        return new WorldSeed
        {
            Mode = GameMode.HotWorld,
            Countries = GetAllCountryISOs().Select(iso => new CountrySeed
            {
                Iso        = iso,
                Tension    = HOT_WORLD_TENSIONS.GetValueOrDefault(iso, 35),
                Stage      = HOT_WORLD_TENSIONS.GetValueOrDefault(iso, 35) >= 70 ? 3 :
                             HOT_WORLD_TENSIONS.GetValueOrDefault(iso, 35) >= 50 ? 2 : 2,
                DemandType = "open"
            }).ToList(),
            StartingTracks = new WorldTracks
            {
                MarketHeat    = 55,
                CivilianCost  = 45,
                Stability     = 50,
                SanctionsRisk = 30,
                GeoTension    = 55
            }
        };
    }

    // --- Mode 5: Custom ---
    private WorldSeed BuildCustomSeed(CustomScenarioConfig config)
    {
        // Merge custom config over a blank slate base
        var base_ = BuildBlankSlateSeed();
        foreach (var override_ in config.CountryOverrides)
        {
            var country = base_.Countries.FirstOrDefault(c => c.Iso == override_.Iso);
            if (country != null)
            {
                country.Tension    = override_.Tension;
                country.Stage      = override_.Stage;
                country.DemandType = override_.Stage >= 2 ? "open"
                                   : override_.Stage == 1 ? "covert" : "none";
            }
        }
        base_.StartingTracks = config.StartingTracks ?? base_.StartingTracks;
        base_.Mode = GameMode.Custom;
        base_.ScenarioName = config.ScenarioName;
        return base_;
    }

    // Pre-defined regional tension values for Hot World mode
    private static readonly Dictionary<string, int> HOT_WORLD_TENSIONS = new()
    {
        // Middle East — Stage 3
        { "SYR", 70 }, { "YEM", 70 }, { "IRQ", 70 }, { "PSE", 70 }, { "LBY", 70 },
        // Sub-Saharan Africa — Stage 3
        { "SDN", 70 }, { "SSD", 70 }, { "ETH", 70 }, { "COD", 70 }, { "MLI", 70 },
        { "NER", 70 }, { "BFA", 70 }, { "SOM", 70 }, { "MOZ", 70 }, { "CAF", 70 },
        // South/Southeast Asia — Stage 3
        { "AFG", 70 }, { "MMR", 70 }, { "PAK", 70 },
        // Eastern Europe — Stage 2
        { "UKR", 50 }, { "GEO", 50 }, { "MDA", 50 }, { "ARM", 50 }, { "AZE", 50 },
        // Central Asia — Stage 2
        { "TJK", 50 }, { "KGZ", 50 },
        // Latin America — Stage 2
        { "VEN", 50 }, { "COL", 50 }, { "MEX", 50 }, { "HND", 50 }, { "GTM", 50 },
        // North Korea / Taiwan tension — Stage 2
        { "PRK", 50 },
        // All others default to 35 (Stage 2) via GetValueOrDefault above
    };
}
```

### 8.2 GameMode Enum (ArmsFair.Shared)

```csharp
// ArmsFair.Shared/Enums/GameMode.cs
public enum GameMode
{
    Realistic  = 0,   // ACLED + GPI seeded
    EqualWorld = 1,   // all Stage 1, tension 25
    BlankSlate = 2,   // all Stage 0, tension 5
    HotWorld   = 3,   // regional Stage 2-3 by pre-defined groupings
    Custom     = 4    // host-configured
}
```

### 8.3 CustomScenarioConfig

```csharp
// ArmsFair.Shared/Models/CustomScenarioConfig.cs
public class CustomScenarioConfig
{
    public string ScenarioName { get; set; } = "Custom Scenario";
    public string? ScenarioCode { get; set; }     // shareable code e.g. "SCN-4X7K"
    public List<CountryOverride> CountryOverrides { get; set; } = new();
    public WorldTracks? StartingTracks { get; set; }
}

public class CountryOverride
{
    public string Iso     { get; set; } = string.Empty;
    public int    Stage   { get; set; }
    public int    Tension { get; set; }
}
```

### 8.4 Scenario Sharing

Custom scenarios can be saved to the database and shared via a 8-character code. Any host can load a saved scenario by entering the code in the lobby.

```sql
-- Scenarios table (new)
CREATE TABLE scenarios (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    creator_id    UUID REFERENCES players(id),
    scenario_code VARCHAR(8) UNIQUE NOT NULL,  -- e.g. "SCN-4X7K"
    name          VARCHAR(64) NOT NULL,
    config        JSONB NOT NULL,              -- serialized CustomScenarioConfig
    play_count    INTEGER DEFAULT 0,
    created_at    TIMESTAMPTZ DEFAULT NOW()
);
```
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

---

## 9. Authentication and Account System

### 9.1 Overview
Authentication uses JWT (JSON Web Tokens). Every client — whether browser or Steam — authenticates once via REST before connecting to the SignalR hub. The JWT is attached to the hub connection and validated on every message. The server never trusts the client's claimed identity — the JWT is the only authority.

### 9.2 REST Auth Endpoints

```csharp
// Program.cs — minimal API auth endpoints
app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth) =>
{
    var result = await auth.RegisterAsync(req.Username, req.Email, req.Password);
    return result.Success
        ? Results.Ok(new AuthResponse(result.Token, result.Profile))
        : Results.BadRequest(result.Error);
});

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth) =>
{
    var result = await auth.LoginAsync(req.UsernameOrEmail, req.Password);
    return result.Success
        ? Results.Ok(new AuthResponse(result.Token, result.Profile))
        : Results.Unauthorized();
});

app.MapPost("/api/auth/steam", async (SteamAuthRequest req, AuthService auth) =>
{
    // Validates Steam session ticket with Steam Web API
    // then finds or creates a player account linked to that Steam ID
    var result = await auth.LoginSteamAsync(req.SessionTicket, req.SteamId);
    return result.Success
        ? Results.Ok(new AuthResponse(result.Token, result.Profile))
        : Results.Unauthorized();
});

app.MapGet("/api/auth/me", async (HttpContext ctx, IAccountRepository repo) =>
{
    var playerId = ctx.User.FindFirst("sub")?.Value;
    if (playerId == null) return Results.Unauthorized();
    var profile = await repo.GetProfileWithStatsAsync(playerId);
    return Results.Ok(profile);
}).RequireAuthorization();

app.MapPatch("/api/auth/username", async (ChangeUsernameRequest req,
    HttpContext ctx, AuthService auth) =>
{
    var playerId = ctx.User.FindFirst("sub")?.Value;
    var result = await auth.ChangeUsernameAsync(playerId, req.NewUsername);
    return result.Success ? Results.Ok() : Results.BadRequest(result.Error);
}).RequireAuthorization();
```

### 9.3 AuthService

```csharp
// AuthService.cs
public class AuthService
{
    private readonly IAccountRepository _repo;
    private readonly IConfiguration _config;

    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        // Validate username: 3-20 chars, alphanumeric + underscore + hyphen
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_-]{3,20}$"))
            return AuthResult.Fail("Invalid username format");

        // Check uniqueness
        if (await _repo.UsernameExistsAsync(username))
            return AuthResult.Fail("Username already taken");

        if (await _repo.EmailExistsAsync(email))
            return AuthResult.Fail("Email already registered");

        // Hash password — never store plain text
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

        var player = await _repo.CreatePlayerAsync(username, email, hash);
        var token = MintJwt(player.Id, player.Username);

        return AuthResult.Ok(token, player.ToProfile());
    }

    public async Task<AuthResult> LoginSteamAsync(string sessionTicket, string steamId)
    {
        // Validate ticket with Steam Web API
        var isValid = await ValidateSteamTicketAsync(sessionTicket, steamId);
        if (!isValid) return AuthResult.Fail("Invalid Steam session");

        // Find existing account or create new one
        var player = await _repo.FindBySteamIdAsync(steamId)
                  ?? await _repo.CreateSteamPlayerAsync(steamId);

        var token = MintJwt(player.Id, player.Username);
        return AuthResult.Ok(token, player.ToProfile());
    }

    public async Task<AuthResult> ChangeUsernameAsync(string playerId, string newUsername)
    {
        var player = await _repo.GetByIdAsync(playerId);

        // Enforce 30-day cooldown
        if (player.UsernameChangedAt.HasValue &&
            DateTime.UtcNow - player.UsernameChangedAt.Value < TimeSpan.FromDays(30))
            return AuthResult.Fail("Username can only be changed once every 30 days");

        if (await _repo.UsernameExistsAsync(newUsername))
            return AuthResult.Fail("Username already taken");

        await _repo.UpdateUsernameAsync(playerId, newUsername);
        return AuthResult.Ok();
    }

    private string MintJwt(string playerId, string username)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["JWT:Secret"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", playerId),
                new Claim("username", username)
            },
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 9.4 SignalR Hub — JWT Attachment

```csharp
// SignalRClient.cs (Unity) — attach JWT to connection
_connection = new HubConnectionBuilder()
    .WithUrl($"wss://api.armsfair.game/gamehub?room={roomCode}", options =>
    {
        options.AccessTokenProvider = () =>
            Task.FromResult(PlayerPrefs.GetString("auth_token"));
    })
    .WithAutomaticReconnect()
    .Build();
```

```csharp
// GameHub.cs (server) — extract player identity from JWT
public override async Task OnConnectedAsync()
{
    var playerId = Context.UserIdentifier; // set automatically from JWT sub claim
    var username = Context.User?.FindFirst("username")?.Value;

    var roomCode = Context.GetHttpContext()?.Request.Query["room"].ToString();
    if (roomCode == null) { Context.Abort(); return; }

    var game = await _games.FindByRoomCodeAsync(roomCode);
    if (game == null) { Context.Abort(); return; }

    // Store context for use in hub methods
    Context.Items["gameId"] = game.Id;
    Context.Items["playerId"] = playerId;

    await Groups.AddToGroupAsync(Context.ConnectionId, game.Id);
    await base.OnConnectedAsync();
}
```

---

## 10. Player Statistics — Server Implementation

### 10.1 Stats Update Flow
Stats are updated server-side at game end, during the debrief phase. The client never writes stats directly — all stat changes come through the server after game state is fully resolved.

```csharp
// StatsService.cs
public class StatsService
{
    private readonly IAccountRepository _repo;

    public async Task UpdateAllPlayerStatsAsync(string gameId)
    {
        var game = await _repo.GetCompletedGameAsync(gameId);
        var actions = await _repo.GetAllActionsAsync(gameId);
        var events = await _repo.GetAllEventsAsync(gameId);

        foreach (var player in game.Players)
        {
            var result = BuildGameResult(player, game, actions, events);
            await UpdateSinglePlayerStatsAsync(player.PlayerId, result);
        }
    }

    private async Task UpdateSinglePlayerStatsAsync(string playerId, GameResult result)
    {
        // EF Core — load, update, save in a single transaction
        var stats = await _repo.GetOrCreateStatsAsync(playerId);

        stats.GamesPlayed++;
        stats.GamesWon              += result.IsWinner ? 1 : 0;
        stats.WarsStarted           += result.WarsStartedThisGame;
        stats.FailedStatesCaused    += result.FailedStatesCausedThisGame;
        stats.SmallArmsSold         += result.SmallArmsSoldThisGame;
        stats.VehiclesSold          += result.VehiclesSoldThisGame;
        stats.AirDefenseSold        += result.AirDefenseSoldThisGame;
        stats.DronesSold            += result.DronesSoldThisGame;
        stats.TotalProfitEarned     += result.ProfitThisGame;
        stats.TotalCivilianCost     += result.CivilianCostThisGame;
        stats.CeasefiresBrokered    += result.CeasefiresBrokeredThisGame;
        stats.CoupsFunded           += result.CoupsFundedThisGame;
        stats.CoupsSucceeded        += result.CoupsSucceededThisGame;
        stats.TimesWhistleblown     += result.TimesWhistleblownThisGame;
        stats.TimesWhistleblower    += result.TimesWhistleblowerThisGame;
        stats.TotalWarParticipations += result.EndingType == "total_war" ? 1 : 0;
        stats.WorldPeaceAchieved    += result.EndingType == "world_peace" ? 1 : 0;
        stats.CompanyCollapses      += result.CompanyCollapsedThisGame ? 1 : 0;
        stats.ReconstructionWins    += result.ReconstructionWinsThisGame;
        stats.LegacyScoreTotal      += result.LegacyScoreThisGame;
        stats.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveStatsAsync(stats);
    }

    private GameResult BuildGameResult(GamePlayer player, CompletedGame game,
        List<PlayerAction> actions, List<GameEvent> events)
    {
        var playerActions = actions.Where(a => a.GamePlayerId == player.Id).ToList();

        return new GameResult
        {
            IsWinner              = game.WinnerPlayerId == player.PlayerId,
            ProfitThisGame        = player.Capital - 50, // net from starting $50M
            CivilianCostThisGame  = CalculateCivCostContribution(player.Id, playerActions),
            SmallArmsSoldThisGame = playerActions.Count(a => a.WeaponCategory == "small_arms"),
            // ... all other fields
            LegacyScoreThisGame   = CalculateLegacyScore(player, game, playerActions),
            EndingType            = game.EndingType
        };
    }
}
```

---

## 11. Text Chat — Server Architecture

### 11.1 GameHub Chat Methods

```csharp
// GameHub.cs — chat methods added to existing hub
public async Task SendChat(SendChatMessage msg)
{
    var gameId   = Context.Items["gameId"] as string;
    var playerId = Context.Items["playerId"] as string;
    var player   = await _games.GetPlayerAsync(gameId, playerId);

    // Validate
    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text.Length > 280) return;

    // Rate limit check — Redis
    var rateLimitKey = $"chat_rate:{gameId}:{playerId}";
    var count = await _redis.StringIncrementAsync(rateLimitKey);
    if (count == 1) await _redis.KeyExpireAsync(rateLimitKey, TimeSpan.FromSeconds(10));
    if (count > 5)
    {
        await Clients.Caller.SendAsync("Error", new { code = "RATE_LIMITED",
            message = "Slow down — maximum 5 messages per 10 seconds" });
        return;
    }

    // Profanity filter if room has it enabled
    var game = await _games.GetGameAsync(gameId);
    var text = game.Settings.ProfanityFilterEnabled
        ? _profanityFilter.Clean(msg.Text)
        : msg.Text;

    // Persist to database
    var saved = await _chatRepo.SaveMessageAsync(new ChatMessageRecord
    {
        GameId      = gameId,
        SenderId    = playerId,
        RecipientId = msg.RecipientId,
        Text        = text,
        IsPrivate   = msg.RecipientId != null,
        RoundNumber = game.CurrentRound,
        Phase       = game.CurrentPhase.ToString()
    });

    var outgoing = new ChatMessageReceived(
        MessageId:       saved.Id,
        SenderId:        playerId,
        SenderUsername:  player.Username,
        SenderColor:     player.ColorHex,
        Text:            text,
        IsPrivate:       msg.RecipientId != null,
        IsSystem:        false,
        RecipientId:     msg.RecipientId,
        SentAt:          DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        RoundNumber:     game.CurrentRound,
        Phase:           game.CurrentPhase.ToString()
    );

    if (msg.RecipientId != null)
    {
        // Private DM — send only to recipient and sender
        await Clients.User(msg.RecipientId).SendAsync("ChatMsg", outgoing);
        await Clients.Caller.SendAsync("ChatMsg", outgoing);
    }
    else
    {
        // Global — broadcast to entire room
        await Clients.Group(gameId).SendAsync("ChatMsg", outgoing);
    }
}

public async Task MutePlayer(MutePlayerMessage msg)
{
    var gameId   = Context.Items["gameId"] as string;
    var playerId = Context.Items["playerId"] as string;

    await _chatRepo.SaveMuteAsync(gameId, playerId, msg.TargetPlayerId);
    // Mute is client-enforced — server records it but filtering happens on Unity side
    await Clients.Caller.SendAsync("MuteConfirmed", new { msg.TargetPlayerId });
}
```

### 11.2 Chat History for Debrief

```csharp
// ChatRepository.cs
public async Task<List<ChatMessageRecord>> GetGameChatHistoryAsync(
    string gameId,
    bool includePrivate = false)
{
    var query = _db.ChatMessages
        .Where(m => m.GameId == gameId)
        .Where(m => !m.IsPrivate || includePrivate)
        .OrderBy(m => m.SentAt);

    return await query.ToListAsync();
}
```

Global chat history is sent to all players in the debrief `StateSync`. Private chat history is not included — it remains private even after the game ends.

### 11.3 System Messages

The server sends system messages (IsSystem = true) for key game events. These appear in the chat panel in a distinct style:

```csharp
// Helper used by PhaseOrchestrator and event handlers
private async Task BroadcastSystemMessageAsync(string gameId, string text)
{
    await _chatRepo.SaveMessageAsync(new ChatMessageRecord
    {
        GameId   = gameId,
        Text     = text,
        IsSystem = true,
        Phase    = _currentPhase.ToString()
    });

    await Clients.Group(gameId).SendAsync("ChatMsg", new ChatMessageReceived(
        MessageId: Guid.NewGuid().ToString(),
        SenderId: "system",
        SenderUsername: "Game",
        SenderColor: "#AAAAAA",
        Text: text,
        IsPrivate: false,
        IsSystem: true,
        RecipientId: null,
        SentAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        RoundNumber: _currentRound,
        Phase: _currentPhase.ToString()
    ));
}

// Example system messages:
// "Round 3 has begun. Procurement phase opens now."
// "Meridian Arms has been sanctioned by the UN Security Council."
// "A ceasefire has been declared in Sudan."
// "WARNING: Global stability has reached 80. Conflicts are spreading faster."
```

---

## 12. Voice Chat — Vivox Architecture

### 12.1 Overview
Voice chat uses Unity Gaming Services (UGS) Vivox. The server generates signed Vivox access tokens for each player when they need to join a voice channel. The client uses these tokens to authenticate with Vivox's infrastructure directly — audio data never passes through your game server.

```
Player → Game Server → (mint Vivox JWT) → Player
Player → Vivox infrastructure (audio)
Game Server → (voice state messages via SignalR) → Player
```

### 12.2 UGS Setup Required
Before voice chat works, you need:
1. Create a Unity Gaming Services project at dashboard.unity3d.com
2. Enable Vivox in the UGS dashboard
3. Add your Vivox API key and secret to your server environment variables
4. Initialize UGS in Unity via `await UnityServices.InitializeAsync()`

### 12.3 Server — Vivox Token Generation

```csharp
// VivoxTokenService.cs
public class VivoxTokenService
{
    private readonly string _vivoxKey;
    private readonly string _vivoxIssuer;
    private readonly string _vivoxDomain;

    public VivoxTokenService(IConfiguration config)
    {
        _vivoxKey    = config["Vivox:ApiKey"];
        _vivoxIssuer = config["Vivox:Issuer"];      // your Vivox domain
        _vivoxDomain = config["Vivox:Domain"];
    }

    // Called when player requests to join voice for a game room
    public string MintChannelToken(string playerId, string roomCode, string action)
    {
        // action: "join" or "leave"
        var channelUri = $"sip:confctl-g-{roomCode}@{_vivoxDomain}";

        var claims = new Dictionary<string, object>
        {
            { "iss", _vivoxIssuer },
            { "exp", DateTimeOffset.UtcNow.AddSeconds(90).ToUnixTimeSeconds() },
            { "vxa", action },          // vivox action
            { "vxi", Guid.NewGuid().ToString() },  // unique per token
            { "f",   $"sip:.{_vivoxIssuer}.{playerId}.@{_vivoxDomain}" }, // from
            { "t",   channelUri }       // to (channel)
        };

        return JWT.Encode(claims, _vivoxKey, JwtHashAlgorithm.HS256);
    }
}
```

```csharp
// GameHub.cs — voice token request handler
public async Task RequestVoiceToken(string action)
{
    var gameId   = Context.Items["gameId"] as string;
    var playerId = Context.Items["playerId"] as string;
    var game     = await _games.GetGameAsync(gameId);

    // Only issue tokens during appropriate phases
    if (game.CurrentPhase == GamePhase.Sales)
    {
        await Clients.Caller.SendAsync("Error",
            new { code = "VOICE_BLOCKED", message = "Voice disabled during sealed phase" });
        return;
    }

    var token = _vivoxTokens.MintChannelToken(playerId, game.RoomCode, action);
    var channelUri = $"sip:confctl-g-{game.RoomCode}@{_vivoxDomain}";

    await Clients.Caller.SendAsync("VivoxToken", new VivoxTokenMessage(
        AccessToken:       token,
        ChannelUri:        channelUri,
        PushToTalkRequired: game.Settings.PushToTalkRequired
    ));
}
```

### 12.4 Phase-Based Voice State Broadcasting

The PhaseOrchestrator broadcasts voice state changes to all clients when a phase transition occurs. Clients use these messages to show/hide the voice UI and enforce muting.

```csharp
// PhaseOrchestrator.cs — voice state broadcast on phase transition
private async Task BroadcastVoiceStateAsync(string gameId, GamePhase phase, GameSettings settings)
{
    var (isActive, reason) = phase switch
    {
        GamePhase.WorldUpdate  => (false, "world_update"),
        GamePhase.Procurement  => (settings.VoiceInProcurement, "procurement_phase"),
        GamePhase.Negotiation  => (true,  "negotiation_phase"),
        GamePhase.Sales        => (false, "sealed_phase"),
        GamePhase.Reveal       => (false, "reveal_animation"),
        GamePhase.Consequences => (true,  "consequences_phase"),
        _ => (false, "unknown")
    };

    await _hub.Clients.Group(gameId).SendAsync("VoiceState",
        new VoiceStateMessage(isActive, reason));

    // For reveal phase: send a second "active" message after 15 seconds
    if (phase == GamePhase.Reveal)
    {
        await Task.Delay(15_000);
        await _hub.Clients.Group(gameId).SendAsync("VoiceState",
            new VoiceStateMessage(true, "reveal_complete"));
    }
}
```

### 12.5 Environment Variables Required for Voice

Add to server .env / Railway environment:
```
Vivox__ApiKey     — your Vivox API key from UGS dashboard
Vivox__Issuer     — your Vivox issuer (domain identifier)
Vivox__Domain     — your Vivox FQDN (e.g. mt1s.vivox.com)
```

---

## 13. Updated Open Technical Questions

1. **SignalR WebGL compatibility for voice token requests.** The Vivox token is requested via SignalR. Ensure the Unity WebGL build's SignalR client correctly handles the `VivoxToken` message and that the Vivox Unity SDK initializes correctly in a WebGL context. Vivox's WebGL support is less mature than desktop — test early.

2. **Chat persistence during async games.** In async mode (24-hour phases), chat messages span multiple days. Ensure the chat history endpoint paginates efficiently — a 20-round async game could have thousands of messages. Add cursor-based pagination to `GetGameChatHistoryAsync`.

3. **Username profanity check on registration.** The `AuthService.RegisterAsync` validates format but not content. Add a profanity check against a word list before saving. Consider using a maintained .NET package like `ProfanityDetector` rather than a manual word list.

4. **Steam account username generation.** When a Steam player authenticates for the first time, they need a username generated for them (they may not have set one). Generate a username from their Steam display name, slugified and deduplicated: `commander_steel` → `commander_steel_1` if taken. Prompt them to change it in the profile screen.

5. **Vivox free tier limits.** Vivox on UGS free tier supports up to 100 concurrent voice users. For a 6-player game this is more than sufficient at indie scale. Check current UGS pricing before launch — the limit and cost structure may have changed.

6. **Chat moderation.** Consider whether you need a reporting system (players report abusive chat to a moderation queue) or whether muting is sufficient. For a small indie game, muting is likely enough — a full moderation system is significant server-side work.

---

*End of Technical Architecture v0.3*
*Engine: Unity 2023 LTS | Server: ASP.NET Core 8 + SignalR*
*New in v0.3: Auth (Section 9), Stats (Section 10), Text Chat (Section 11), Voice Chat (Section 12)*
