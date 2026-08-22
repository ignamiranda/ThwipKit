# Wayfinder Map: Spider-Man Modding Tool

## Destination

Create a beginner-friendly graphical modding tool for Marvel's Spider-Man Remastered that starts with texture modding (extract → edit → rebuild) and expands to support other asset types over time.

## Notes

- Domain: Game modding, specifically texture workflows for Insomniac's Spider-Man titles
- Skills every session should consult: research (file formats), prototype (UI/workflow), grilling (decisions), domain-modeling
- Standing preferences: Beginner modders, GUI-focused, start with textures, expand to other assets
- Branch convention: `feat/#<ticket>-<desc>` | `fix/#<ticket>-<desc>` | `research/<topic>`

## Decisions so far

- [Texture file formats and archive structure for Spider-Man Remastered](.wayfinder/tickets/001-research-spider-man-texture-formats.md) — Textures use .texture extension with GPU-compressed data (BC1-BC7) stored in asset_archive/TOC and g00sXXX files; no encryption; convertible to/from DDS/PNG
- [Existing texture extraction and rebuilding tools](.wayfinder/tickets/002-research-existing-tools.md) — SMPT (extract/replace), SpiderManTextureTool (.texture↔.dds), SpiderTex (.texture↔.png w/metadata), RawTex (CLI converter), Overstrike (newer mod manager); SMPT has restrictive license
- [Legal and ethical considerations for modding tools](.wayfinder/tickets/003-research-legal-considerations.md) — EULA prohibits modification; avoid DRM circumvention; tool should be for personal offline use only with clear disclaimers; copyright remains with Insomniac/Sony/Marvel
- [Game update compatibility handling](.wayfinder/tickets/007-research-update-compatibility.md) — Implement version detection, backward compatibility, version history, update checking, clear communication, flexible version checking, graceful degradation, and community integration approaches
- [GUI framework selection](.wayfinder/tickets/006-grilling-gui-framework.md) — Windows Forms (WinForms) chosen for simplicity, excellent designer support, and native Windows experience ideal for beginner modders
- [Mod packaging and sharing features](.wayfinder/tickets/008-grilling-mod-packaging.md) — Focus solely on creation workflow (extract → edit → rebuild); leverage existing tools like SMPT/Overstrike for packaging/sharing; prioritize simplicity for beginner modders
- [UI workflow approach](.wayfinder/tickets/005-grilling-ui-workflow.md) — GUI with optional CLI; primary Windows GUI application for beginners with command-line interface for advanced users/power users
- [Texture conversion requirements](.wayfinder/tickets/001-research-spider-man-texture-formats.md) — Support .texture ↔ .dds (BC1-BC7) and .texture ↔ .png (with metadata) conversions using approaches from existing tools
- [Basic texture extraction prototype](.wayfinder/tickets/004-prototype-basic-extraction.md) — Created conceptual prototype demonstrating archive reading, texture extraction, and conversion workflow using existing tools like SpiderTex/RawTex
- [Project setup and basic WinForms application shell](.scratch/spider-man-modding-tool/issues/01-project-setup-winformsshell.md) — Created Windows Forms application with menu/status strip, About dialog, and proper .gitignore
- [Game installation detection and texture listing](.scratch/spider-man-modding-tool/issues/02-gamedetection-texturelisting.md) — Implemented game installation detection (Steam/Epic paths), manual path selection, TOC parsing for texture names, ListBox display, progress indication, and refresh functionality
- [Single texture extraction to PNG](.scratch/spider-man-modding-tool/issues/03-single-texture-extraction-png.md) — Added Extract button, implemented TOC-based texture extraction, temporary file handling, external tool invocation simulation, output directory selection, and proper cleanup
- [Single texture rebuilding from PNG](.scratch/spider-man-modding-tool/issues/04-single-texture-rebuilding-png.md) — Added Rebuild button, implemented PNG file selection, texture conversion simulation, backup system, archive writing with proper offset handling, and cleanup
- [Complete extract → [edit] → rebuild workflow](.scratch/spider-man-modding-tool/issues/05-complete-extract-edit-rebuild-workflow.md) — Added Edit button, implemented external editor launch, workflow status tracking, editor close detection, automatic rebuild prompting, and validation
- [Backup system](.scratch/spider-man-modding-tool/issues/07-backup-system.md) — Implemented automatic backup before rebuild (atomic writes), retention policy, restore from backup, backup validation, GUI controls for enable/disable/max count/directory, and backup list display
- [Command-line interface](.scratch/spider-man-modding-tool/issues/06-commandline-interface.md) — Created SpiderManModdingTool.CLI console app with extract, rebuild, list, backup, restore, version, and help commands; shares Core library with GUI; auto-detects game path or uses SPIDERMAN_GAME_PATH env var
- [Version detection and compatibility](.scratch/spider-man-modding-tool/issues/08-version-detection-compatibility.md) — Created GameVersionDetector with cascading detection (exe, Steam manifest, version files), platform detection, known problematic/good version checking, GUI status bar display + warning dialogs + Help menu info, CLI version warnings before operations
- [Error handling and user feedback](.scratch/spider-man-modding-tool/issues/09-error-handling-userfeedback.md) — Created Logger (file-based, levels, exception logging), ErrorHandler (user-friendly messages, error codes, recovery options), PngValidator (signature/dimension validation); integrated into GUI and CLI for all critical operations
- [Settings persistence and temp files](.scratch/spider-man-modding-tool/issues/10-settings-persistence-tempfiles.md) — Created AppSettings (JSON persistence, game path/backup/window preferences, recent textures, .bak backup) and TempFileManager (unique naming, secure deletion, auto-cleanup on exit); GUI loads/saves settings, File > Clean Temporary Files menu item

## Not yet specified

(None - all major decisions have been made)

## Out of scope

- Non-PC versions of Spider-Man games (console-only features)
- Online multiplayer or cheating-related functionality
- Real-time mod injection or memory editing