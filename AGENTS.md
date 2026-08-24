# AGENTS.md

Build and code facts for the Spider-Man Modding Tool. Complements `CLAUDE.md` (which covers process — issue tracker, triage labels, domain docs).

## Build & Test

```powershell
# Build everything
dotnet build ThwipKit.sln

# Build one project (works without the solution too)
dotnet build ThwipKit.Core/ThwipKit.Core.csproj

# Run all tests
dotnet test ThwipKit.sln

# Run one test project directly
dotnet test ThwipKit.Core.Tests/ThwipKit.Core.Tests.csproj
```

Target framework: **.NET 9**. Projects:

- `ThwipKit.Core` — core library (game abstraction, ArchiveManager, TOC parsing)
- `ThwipKit.CLI` — command-line interface
- `ThwipKit` — WinForms GUI (Windows-only)
- `ThwipKit.Core.Tests` — xUnit tests

Last known green state: all 4 projects build with 0 errors, 17/17 tests pass.

## Non-obvious code facts

**`GameDefinitionLoader` has static state that tests race on.**
The loader uses a static `Dictionary` cleared and replaced by `LoadDefinitions()` and `LoadBuiltInDefinitions()`. Tests that construct `GameMSMR`/`GameI30`/etc. directly depend on this static state. In parallel xUnit runs, one class calling `LoadDefinitions()` with custom definitions can race with another constructing a built-in wrapper, producing intermittent `"Unknown built-in game ID"` failures.
**Fix pattern:** in tests, use `ConfiguredGame(new GameDefinition { ... })` with a locally-constructed definition instead of `new GameMSMR()`. See `ArchiveManagerTests.CreateTestGame`.

**DSAR compression byte mapping is file-format-specific, not game-specific.**
`ArchiveManager.ResolveDsarCompressionType` maps bytes (`3 => Lz4`, `2 => GDeflate`, `0 => None`) from the DSAR block header. This is a property of the DSAR format itself and does NOT belong on `GameBase` or in game profiles. What IS game-specific is which formats a game *supports* — that's `GameDefinition.CompressionFormats`, validated after byte resolution.

**Only zlib (TOC wrapper) and LZ4 (DSAR type 3) have working decoders.**
GDeflate and Zstd are declared in profiles but throw `NotSupportedException` at decode time. `CompressionSupport.IsImplemented` is the single source of truth for whether a decoder exists. Never silently skip or default — throw with a clear message.

**Game detection never silently defaults.**
`GameFactory.CreateGameFromPath` returns typed `Match`/`NoMatch`/`Ambiguous` results. `GameVersionDetector.DetectVersion(string, GameBase)` requires the game profile explicitly. The old parameterless overload was removed to eliminate the silent-MSMR fallback bug.

**The 6 empty Game subclasses are spec-mandated.**
`GameMSMR`, `GameMM`, `GameMSM2`, `GameRCRA`, `GameI30`, `GameI33` are 6-line `ConfiguredGame` wrappers. Do NOT remove them (spec criterion 2). New games are added via a JSON profile in `GameDefinitions/` plus an optional wrapper — `ConfiguredGame` handles all behavior.

**`GameDefinitions/*.json` files are `EmbeddedResource`** in `ThwipKit.Core.csproj`. They ship with the assembly and are loaded by `GameDefinitionLoader.LoadBuiltInDefinitions()`.

## Repository conventions

- **No `.codegraph/` index** — use grep/glob/read for code navigation.
- **Wayfinder planning docs (`.wayfinder/tickets/010-014`) are NOT tracker issues.** The real tracker lives at `.scratch/<feature>/issues/`. See `docs/agents/issue-tracker.md`.
- **Branch naming:** `feat/#<ticket>-<desc>` | `fix/#<ticket>-<desc>` | `research/<topic>`. See `docs/agents/branch-conventions.md`.
- **UI framework:** ticket 010 migrates from WinForms to WPF for complex UIs (tree navigation, drag-and-drop, grid editors). New GUI work should target WPF; the existing WinForms project stays until migration is complete.
