# Arms Fair — Handoff File
**Last updated:** 2026-04-28  
**Plan:** [2026-04-28-server-and-shared.md](2026-04-28-server-and-shared.md)

---

## Session Summary

Unity is installing (Windows + Linux IL2CPP build support). While waiting, the goal is to build the full server-side stack so it is ready to integrate with Unity once installed.

---

## Completed

| Task | Description | Notes |
|------|-------------|-------|
| — | Read all three spec docs | game_spec, technical_architecture, balance |
| — | Created CLAUDE.md | Project context file at repo root |
| — | Created docs/plans/ directory | Plan storage location |
| — | Wrote implementation plan | 19 tasks, saved to docs/plans/2026-04-28-server-and-shared.md |

---

## Not Started

| Task | Description |
|------|-------------|
| 1 | Scaffold .NET 8 solution (replace Godot artifacts) |
| 2 | Enums (SaleType, WeaponCategory, GamePhase, CountryStage) |
| 3 | Balance.cs — all tunable constants |
| 4 | Core models (WorldTracks, CountryState, PlayerProfile, PlayerAction, GameState, PlayerStats) |
| 5 | Message types (ServerMessages, ClientMessages) |
| 6 | TrackEngine + tests |
| 7 | ProfitEngine + tests |
| 8 | SpreadEngine + tests |
| 9 | BlowbackEngine + tests |
| 10 | CoupEngine + tests |
| 11 | EndingChecker + tests |
| 12 | EF Core DbContext + entity classes |
| 13 | Program.cs wiring |
| 14 | GameHub (SignalR) |
| 15 | SeedService (ACLED + GPI) |
| 16 | TickerService |
| 17 | Dockerfile + appsettings + .gitignore |
| 18 | GeoJSON preprocessing script (Python) |
| 19 | Final test run + CLAUDE.md update |

---

## Blockers / Notes

- Unity not yet installed — all Unity client work blocked until install completes
- Existing repo has Godot leftover files: `Arms Fair.csproj`, `Arms Fair.sln`, `test.cs`, `test.cs.uid`, `icon.svg`, `icon.svg.import` — Task 1 removes these
- No git repo initialized yet — Task 1 runs `git init`
- API keys needed before SeedService (Task 15) can be tested end-to-end: ACLED, GPI (hosted JSON), NewsAPI

---

## What's Next

Start Task 1: scaffold the .NET 8 solution.
