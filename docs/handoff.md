# Arms Fair — Session Handoff

## Last updated: 2026-04-29

---

## Current State

### Server (ArmsFair.Server) — commit 7ff999d
The server is fully scaffolded and the core simulation loop is complete. 61/61 tests passing.

**Done:**
- Solution structure: ArmsFair.Shared, ArmsFair.Server, ArmsFair.Server.Tests
- Shared: enums, Balance.cs, all models, all SignalR message types
- Simulation engines: TrackEngine, SpreadEngine, BlowbackEngine, CoupEngine, ProfitEngine, EndingChecker
- GameHub: full SignalR hub (join, action, reveal, consequences, endings, coup, treaty, whistle, peacekeeping)
- SeedService: ACLED + GPI fetch, 195-country base list, Redis cache, all 5 game modes
- TickerService: background phase timer, 1s tick, registered as hosted service
- **PhaseOrchestrator**: single authority for phase transitions — runs SpreadEngine on WorldUpdate, Reveal/Consequences logic on Reveal entry, EndingChecker after every advance. GameHub delegates entirely to it.
- **AuthService**: JWT mint/validate + BCrypt (work factor 12). Register, login, session restore.
- **PlayerEntity**: `players` table — username, email, password hash, Steam ID, home nation. Unique indexes on username/email/steamId. Added to ArmsFairDb.
- **REST auth endpoints**: POST /api/auth/register, POST /api/auth/login, GET /api/auth/me
- EF Core entities: GameSession, PlayerStat, AuditLog
- Dockerfile, railway.toml, appsettings.Production.json
- `ArmsFair.Server/GeoData/adjacency.json` — server-side copy for spread engine

**NOT yet done (server):**
- EF Core migrations (no `dotnet ef migrations add` has been run — DB schema not created yet)
- StatsService — lifetime stat updates at game end
- ChatRepository — persist chat messages to DB
- AccountRepository — CRUD wrapper around PlayerEntity
- VivoxTokenService — mint Vivox access tokens for voice chat
- PhaseOrchestrator: treaty system (signatories, compliance checks) is stubbed (0 values)
- PhaseOrchestrator: player capital/reputation mutation on Consequences (profitUpdates computed but not applied back to PlayerProfile in GameState)
- Lobby REST endpoints (room browser, create/join room via HTTP before SignalR)

### Unity Client (ArmsFair/) — various commits
The globe scene is functional. Click detection, zoom, and country highlighting work via WPMapGlobeEditionLite.

**Done:**
- MapGlobe scene: WPM globe, click detection, auto-rotation, zoom
- Bootstrap scene: entry point
- GlobeRenderer, GlobeCameraController, GlobeTensionBridge, ViewToggleManager
- CountryOverlay shader, CountryID texture, adjacency/countries GeoData
- WPMGlobeBridge.cs: bridge between WPM and game logic

**NOT yet done (Unity):**
- SignalRClient.cs — WebSocket connection to server
- MessageHandler.cs — routes incoming SignalR messages
- GameManager.cs, PhaseManager.cs — game state on client
- AccountManager.cs, AuthApiClient.cs — login/register UI + REST calls
- ChatManager.cs, ChatPanel.cs — chat UI
- VoiceChatManager.cs — Vivox voice
- All UI: HUD, ProcurementScreen, NegotiationPanel, RevealAnimator, DebriefScreen, Leaderboard, LobbyScreen, ProfileScreen
- FlatMapRenderer.cs, ArcLineRenderer.cs
- Scenes: Login, Lobby, Main, MapFlat
- GlobeAtmosphere.shader, ArcLine.shader

---

## Known Issues / Gotchas

### WPM Scroll Wheel Patch
`WPMInternal.cs` line ~553: scroll wheel read must be unconditional (not gated on `mouseIsOver`).
```csharp
{
    float wheel = input.GetAxis("Mouse ScrollWheel");
    wheelAccel += wheel;
}
```

### PhaseOrchestrator — Consequences Not Applied to PlayerProfile
`PhaseOrchestrator.RunRevealAsync` computes `profitUpdates` and `repUpdates` but does NOT mutate `PlayerProfile.Capital` or `PlayerProfile.Reputation` in the returned `GameState`. This needs to be wired up — the Consequences message is broadcast correctly but the authoritative state is not updated.

### No EF Migrations
The `players` table and the existing game tables have never had `dotnet ef migrations add` run. Before deploying, run:
```
dotnet ef migrations add InitialSchema --project ArmsFair.Server
dotnet ef database update --project ArmsFair.Server
```

### PlayerStatEntity vs PlayerEntity
`PlayerStatEntity` = per-game stats snapshot (existing).
`PlayerEntity` = persistent player account (new, added this session).
These are separate — `PlayerEntity` has no FK to `PlayerStatEntity` yet.

---

## Pending Work (priority order)

1. **EF Core migrations** — must happen before any real deployment
2. **Apply Consequences to GameState** — PhaseOrchestrator needs to update PlayerProfile.Capital/Reputation after computing profit/rep deltas
3. **Lobby REST endpoints** — room browser, create/join room (currently only possible via SignalR)
4. **StatsService** — update lifetime stats at game end
5. **Unity: SignalRClient + MessageHandler** — the networking layer that makes the client functional
6. **Unity: AccountManager + AuthApiClient** — login/register flow
7. **Unity: GameManager + PhaseManager** — client-side game state machine
8. **Unity: UI screens** — LobbyScreen, ProcurementScreen, NegotiationPanel, RevealAnimator, DebriefScreen
9. **ChatRepository + chat persistence** — save messages to DB
10. **VivoxTokenService** — voice chat token minting
