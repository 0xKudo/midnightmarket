# Arms Fair — Plans Handoff
**Last updated:** 2026-04-30

---

## Session Summary (2026-04-30)

Completed smoke test (Task 14 of UI placeholder plan). Full login → lobby → create room → start game → HUD flow verified in Unity Editor play mode. Multiple server-side and client-side bugs fixed along the way (see docs/handoff.md for full list). The game is now playable to the point of seeing the HUD with world tracks rendered.

---

## Plans Status

| Plan | Status |
|------|--------|
| 2026-04-28-globe-rendering.md | ✅ Complete |
| 2026-04-28-server-and-shared.md | ✅ Complete |
| 2026-04-29-phase-orchestrator-auth.md | ✅ Complete |
| 2026-04-29-consequences-migrations-lobby.md | ✅ Complete |
| 2026-04-29-signalr-client-setup.md | ✅ Complete |
| 2026-04-29-client-game-loop.md | ✅ Complete |
| 2026-04-29-ui-placeholder.md | ✅ Tasks 1–14 complete (smoke test passed) |

---

## Completed (previous sessions)

See individual plan files for full task breakdowns. All server simulation, auth, lobby, SignalR client, and UI placeholder screens are complete.

---

## Completed (2026-04-30 session)

### Task 14 — Smoke Test (UI Placeholder Plan)

Full end-to-end smoke test passed after fixing the following:

**Server fixes:**
- JWT `sub` → `ClaimTypes.NameIdentifier` remapping: fixed in `AuthService.ValidateTokenAsync` and all `Program.cs` room endpoint `ctx.User.FindFirst("sub")` calls
- Redis hard dependency: `IConnectionMultiplexer` made optional in `SeedService` constructor and `Program.cs` startup — server runs without Redis locally
- `CreateGame` hub method wired as the game-start trigger (replaces the disconnected `StartGame` flow)

**Client fixes:**
- All 11 screen TemplateContainers: added `position=Absolute, left/top/right/bottom=0` in C# Awake (all screens were invisible without this)
- `serverUrl` changed from `[SerializeField]` to `const` in `NetworkManagerBootstrap` — prevents stale scene serialization
- LoginScreen server-ready gating: form disabled until server responds; `IsServerReady` static flag for late subscribers
- `GameClient.InvokeAsync`: auto-reconnects if hub is in `Disconnected` state
- `PreGameLobbyScreen.OnStart`: calls `CreateGameAsync` (hub) instead of `StartGameAsync` — `CreateGame` initialises game state and returns `StateSync`
- `JoinGameAsync` now sets `GameClient.GameId` locally before invoking hub
- Game mode dropdown corrected: Realistic, Equal World, Blank Slate, Hot World, Custom / Scenario
- `placeholder.uss`: `.unity-text-field__input` rules added for readable text inputs
- `LobbyScreen.uxml`: `screen-root` gets `width: 100%; height: 100%`
- `LobbyScreen.cs`: `LobbyApiClient` URL fallback corrected to port 5002

**Infrastructure:**
- PostgreSQL set up locally: DB=`armsfair`, user=`armsfair`, password=`changeme`
- EF Core migration applied: `dotnet ef database update` ✅
- Server runs on port 5002: `dotnet run --urls "http://localhost:5002"`
- DevServerLauncher.cs disabled

---

## What's Next

1. **Fix GAME_NOT_FOUND warning** — remove `JoinGameAsync(room.roomId)` from `PreGameLobbyScreen.Show()`; `CreateGame` already adds host to hub group
2. **HUD data binding** — Phase, Round, timer, leaderboard from `StateSync` / `PhaseStart` messages
3. **Player profile in GameState.Players** — `CreateGame` must include the host's profile
4. **Full game loop** — design and implement procurement → sales → reveal → consequences → debrief
5. **Non-host join flow** — joining players must call `JoinGameAsync` with the hub game ID (not REST roomId) after the host creates the game
6. **LobbyService.MarkStarted** — call on game start
7. **ArcLineRenderer, FlatMapRenderer**
8. **StatsService, ChatRepository, VivoxTokenService**
