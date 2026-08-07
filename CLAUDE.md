# MATR Project (Touken Ranbu Automation Assistant)

## Project Structure

```
MATR/
├── assets/
│   ├── interface.json               # Project entry config (resource version, task definitions, pipeline references)
│   └── resource/                    # MaaFW resource pack
│       ├── base/
│       │   ├── pipeline/            #   Pipeline definitions (JSON)
│       │   ├── custom/              #   Custom actions (C# scripts, one action class per file)
│       │   ├── image/               #   Template match images (1280×720 baseline)
│       │   └── model/               #   OCR model files
│       ├── logo/                    #   Startup logo resources
│       ├── silhouette/              #   Silhouette recognition reference images
│       └── announcement/            #   Version release notes (Markdown)
├── _src/                        # C# source code (Avalonia desktop app)
│   ├── MFAAvalonia/             #   Core library: Models, ViewModels, Views, Services, Controls, etc.
│   ├── MFAAvalonia.Desktop/     #   Desktop host project (MATR.exe entry)
│   └── MFAAvalonia.Android/     #   Android host project
├── docs/                        # Project docs (task design, usage conventions, dev logs, etc.)
├── tools/                       # Build/release scripts (clean_build.ps1, compress_json.py, pack.ps1)
├── runtimes/                    # .NET native runtime libraries (multi-platform, multi-arch; distributed locally, not committed to git)
└── .github/                     # Issue templates
```

> The following directories are auto-generated at runtime and excluded in `.gitignore`: `config/`, `debug/`, `logs/`, `temp/`, `backup/`, `libs/`, `plugins/`.

### Upstream Source Customizations

`_src/` is a second-development fork of [MFAAvalonia](https://github.com/SweetSmellFox/MFAAvalonia). The following lists MATR's customizations over upstream:

| Customization | Files involved | Description |
|---|---|---|
| Removed `agent/` directory | `AppPaths.cs`, `VersionChecker.cs`, `PendingUpdateDeletionHelper.cs` | MATR does not use the Python agent; removed related path creation and update logic |
| Auto-generate pipeline coordinates on startup | `Program.cs` | Generates coordinates dynamically via `pipeline_gen.py` at startup based on the user's screen resolution |
| Global options not shown as task items | `TaskLoader.cs` | Global options are controlled via the settings panel and not listed in the task list |
| Replaced EXE icon | `MFAAvalonia.Desktop/` | Uses MATR custom icon instead of the upstream default |
| Added utility tools to left sidebar | `_src/MFAAvalonia/` | Integrated silhouette recognition, dev logs and other utility entries into the left sidebar |
| Adjusted task list sidebar width | `_src/MFAAvalonia/` | Modified the sidebar width to fit Chinese display and usage habits |

> When upgrading the MFAAvalonia upstream version, verify whether the changes above are affected.

### Key Directory Notes

| Directory | Purpose | When to modify |
|---|---|---|
| `resource/base/pipeline/` | Pipeline definitions, one JSON per task | Adding/modifying task flows |
| `resource/base/custom/` | Custom action C# scripts | Writing a new action for non-standard operations |
| `resource/base/image/` | Template match images, based on 1280×720 | Adding template images for new recognition nodes |
| `resource/base/model/` | PaddleOCR models | Placing models when OCR recognition is needed |
| `_src/MFAAvalonia/` | Core C# code | Modifying program features, UI, config |

### Custom Actions

Current custom actions are located in `resource/base/custom/`, 7 in total:

| File | Purpose |
|---|---|
| `TeamSwitchAction.cs` | Team switching |
| `CaptainDamageAction.cs` | Captain damage handling |
| `DamageLogAction.cs` | Damage logging |
| `DungeonFloorSelectAction.cs` | Dungeon floor selection |
| `ExpeditionMapSelectAction.cs` | Expedition map selection |
| `DispatchLogAction.cs` | Chore (naiban) logging |
| `StopOnDamageAction.cs` | Stop on damage |

Follow the naming and structure of existing files when adding a custom action: one file per action class.

### Pipeline Task List

| File | Task |
|---|---|
| `Sortie.json` | Sortie (battlefield) |
| `Expedition.json` | Expedition |
| `Underground.json` | Underground (Osaka Castle) |
| `Disassemble.json` | Disassemble (arms disposal) |
| `FlowerBrush.json` | Flower brush (morale recovery) |
| `GoHome.json` | One-click return home |
| `LRentaisen.json` | Mock drills (rentaisen) |
| `Mix.json` | Mix (sword fusion) |
| `TacticalTraining.json` | Tactical training |

## AI Response Guidelines

When the user makes the following requests, the AI should follow the corresponding default approach:

| User request | Default AI approach |
|---|---|
| "Fix unstable node" | Add intermediate recognition nodes or adjust recognition thresholds/ROIs |
| "Retry on failure" | Analyze the root cause (which node, which recognition mismatch) and fix the node; never add blind retries |
| "Write a pipeline" | Ask the user for screenshots, ROIs, and screen transition info before writing; never fabricate coordinates |
| "Write a custom action" | Follow the naming and structure of existing files in `resource/base/custom/`; one class per file |

## Coding Style

### C# (under `_src/`)

- .NET 10.0, C# 14, Nullable enabled
- File-scoped namespace declarations (`namespace MFAAvalonia.Helper;`)
- 4-space indentation, PascalCase public members, `_camelCase` private fields
- Log via `LoggerHelper`; avoid `Console.WriteLine`

### JSON (Pipeline / Resource Config)

- 4-space indentation
- No `target_offset`; all coordinate offsets are expressed directly in the `target` array
- Every node must set `on_error`
- All coordinates, ROIs, and template images are based on the **1280×720** base resolution
- The Chinese term 节点 is strictly forbidden; always use the English word "node"
- Use English parent node / child node / sibling node for hierarchy; Chinese kinship terms such as 父节点 / 子节点 / 兄弟节点 are forbidden
- Resource paths use forward slashes `/`

### Source File Encoding

- `.ps1` files: UTF-8 with BOM
- All other source files (`.cs`, `.json`, `.md`, etc.): UTF-8 without BOM

## Build & Common Commands

| Command | Purpose |
|---|---|
| `dotnet build _src/MFAAvalonia.sln` | Build the whole solution |
| `dotnet publish _src/MFAAvalonia.Desktop` | Publish the desktop version |
| `pwsh tools/clean_build.ps1` | Clean build output |
| `python tools/compress_json.py` | Compress JSON files |
| `pwsh tools/pack.ps1` | Package a release |

## Commit Conventions

Follow [Conventional Commits](https://www.conventionalcommits.org/zh-hans/) v1.0.0:

| Type | Use case |
|---|---|
| `feat` | New feature (task, node, recognition logic) |
| `fix` | Bug fix |
| `perf` | Performance optimization |
| `refactor` | Code refactoring (non-functional, non-fix) |
| `docs` | Documentation-only change |
| `style` | Formatting, whitespace (no semantic change) |
| `chore` | Dependency updates, build scripts, maintenance |

> **The AI must not run `git commit` or `git push` on its own** unless the user explicitly asks to commit.

## Branch Strategy

- **`main`**: stable release branch. Direct commits are only allowed for:
  - Small-scope fixes (doc corrections, single-node adjustments, config updates)
  - Urgent bug fixes
- **`develop`**: daily development branch. All new features, multi-node flow changes, and new logic that needs testing should be developed on `develop` and merged into `main` after verification
- Complex features can be developed on a `feat/<feature-name>` branch based on `develop`, then merged back into `develop`

## Review Checklist

When modifying code or pipeline, confirm:

- [ ] JSON fields conform to the MaaFW protocol; no typos or unsupported properties
- [ ] No `target_offset`; all coordinates are expressed directly in the `target` array
- [ ] Every node has `on_error` set
- [ ] The `next` list covers all possible following screens so the correct node is hit in the first recognition cycle
- [ ] Coordinates, ROIs, and template images are based on the 1280×720 baseline
- [ ] New custom actions are placed in `resource/base/custom/`; the file name equals the action class name
- [ ] Pipeline, interface.json, and resource files stay consistent
- [ ] Abnormal interruptions (popups, unexpected dialogs) have handling paths

## Version Numbering (SemVer 2.0.0)

All version numbers follow [Semantic Versioning](https://semver.org/lang/zh-CN/) `MAJOR.MINOR.PATCH`.

### MFAAvalonia Application

- Version is defined in `ApplicationVersion` in `_src/MFAAvalonia/MFAAvalonia.csproj` and the `Version` property in `_src/MFAAvalonia/ViewModels/Windows/RootViewModel.cs`; **both must stay in sync**
- The application itself is updated infrequently; bump manually at release time

### Resource Version

- Version is defined in the `Version` field of `interface.json` at the resource pack root
- Increment rules:

| Increment | Trigger | Example |
|---|---|---|
| **PATCH** | Bug fixes, fine-tuning recognition thresholds/ROIs/timing | `1.2.3 → 1.2.4` |
| **MINOR** | Adding nodes/tasks/recognition logic, restructuring tasks; backward compatible | `1.2.3 → 1.3.0` |
| **MAJOR** | Large-scale rewrite that overhauls the entire flow (rare) | `1.2.3 → 2.0.0` |

- Daily renaming/deleting of node names and reorganizing tasks are **MINOR**-level changes; they do not trigger a MAJOR bump
- PATCH resets to zero when MINOR is bumped; MINOR and PATCH both reset to zero when MAJOR is bumped
- A resource at `0.y.z` is considered in development; release `1.0.0` once stable

## Release Process

Files must be copied manually after `dotnet publish` before running:

```
# Core library (incl. TaskOptionGenerator, TaskQueueView, etc.)
cp _src/MFAAvalonia/bin/Release/net10.0/MFAAvalonia.Core.dll runtimes/libs/

# Desktop host
cp _src/bin/AnyCPU/Release/publish/MATR.dll ./
cp _src/bin/AnyCPU/Release/publish/MATR.exe ./
```

> ⚠️ `_src/bin/AnyCPU/Release/MFAAvalonia.Core.dll` is a stale copy cached by the desktop project; its file size differs from `_src/MFAAvalonia/bin/Release/net10.0/`. **Always copy from the project's own output directory.**
