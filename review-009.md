# Code Review: Ticket 009 (Game Abstraction Layer)

## Standards

### Hard issues (fixed)
- **Duplicated Code — format→parse switch** in TocParser.ParseDat1 and ConfiguredGame.HandleSection. Fixed: extracted `TocParser.ParseSectionData`, ConfiguredGame now delegates.
- **IndexOutOfRangeException** in ArchiveManager.ReadFromDsar — `ReadBytes(4)` not length-checked before indexing. Fixed: added `magic.Length < 4` guard.

### Hard issues (deferred)
- **Speculative Generality + Middle Man — six empty Game subclasses.** GameMSMR–GameI33 are byte-identical wrappers that override nothing. However, spec criterion 2 explicitly calls for "6 concrete classes are thin wrappers," so they stay by design.
- **Shotgun Surgery — section tags duplicated** across six JSONs and ArchiveManager.MsmrSectionTags. Larger refactor to make ArchiveManager profile-driven; deferred.
- **Data Clump — compression bools vs array** in GameDefinition. Touches all JSONs; deferred.
- **Duplicated test fixtures** between GameAbstractionTests and ArchiveManagerTests. Lower priority; deferred.

### Judgement calls (noted, not fixed)
- **Primitive Obsession** — `"ZlibDat1"` and `"KeyValue"` magic strings. Consider enums in future.
- **Divergent Change** — GameVersionDetector bakes MSMR-specific version lists. Pre-existing, outside ticket 009 scope.
- **Thread safety** — GameDefinitionLoader double-load under contention is benign.

## Spec

### Requirements met (verified correct)
- Criterion 5: TocData typed sections ✓
- Criterion 6: Section parsers (72/8/12/8-byte) with strict divisibility ✓
- Criterion 7: Thread-safe loader with load-outside-lock ✓
- Criterion 8: 8-byte wrapper header (AF 12 AF 77 + length) ✓
- Criterion 9: Epic .egstore directory detection ✓
- Criterion 10: DSAR LZ4-only, GDeflate throws NotSupportedException ✓
- Criterion 11: JSON definitions as EmbeddedResource ✓

### Partial / needs future work
- **Criterion 2 partial — ArchiveManager not profile-driven.** Hardcodes TOC path and MsmrSectionTags instead of consuming GameBase. Larger refactor; deferred.
- **Criterion 3 partial — CompressionSupport not used in DSAR path.** ArchiveManager.ReadFromDsar hardcodes byte values 3/2 instead of consulting CompressionSupport. Pre-existing code; deferred.
- **Criterion 4 spirit — GameVersionDetector.DetectVersion(string) silently defaults to MSMR.** Pre-existing overload, not part of ticket 009 changes. The profile-aware overload (added by ticket 009) is clean.

### Scope creep (noted, not blocking)
- `HandleSection`, `GetVersionSpecificBehaviors`, `SupportsFeature`, `HashTableParser`, `DetectGameFromPath`, `CreateGameFromExecutable` go beyond the stated acceptance criteria. Retained for extensibility.

## Summary
- **Standards**: 2 hard issues fixed, 4 deferred, 3 judgement calls noted
- **Spec**: 7/11 criteria verified correct, 3 partial (pre-existing code), 1 scope creep noted
- Worst Standards issue: duplicated section-tag shotgun surgery (deferred — needs ArchiveManager refactor)
- Worst Spec issue: ArchiveManager not profile-driven (deferred — needs architectural change)
