# Agent guide for Bibimcad

Read this first, then `handbook.md` (install and upgrade procedure) and `docs\architecture.md` (how the code fits together). `readme.md` is the short version for a person.

## What this is

A C# add-in for Autodesk Revit 2023 that turns a K1 floor plan DXF into Revit walls:

```
01 Inspect → 02 Coordinates → 03 Read DXF → (set wallLayers) → 04 Classify → 05 Review → 06 Generate → 07 Validate
08 Help explains problems in the current project at any time.
```

All project data lives in one file, `C:\Bibimcad\data\project_state.json` (schema `1.1`). The log is `C:\Bibimcad\logs\Bibimcad.log`.

## Repository layout

| Path | Contents |
| --- | --- |
| `Bibimcad.sln` | Solution with three projects. |
| `src\Bibimcad.Core` | `net48`, no Revit references. Models, `JsonProjectRepository`, DXF reading (netDxf), wall classification, merge, review, CSV, geometry checks, link size check, help advisor. |
| `src\Bibimcad.Addin` | `net48`, WPF. `App`, `RibbonBuilder`, `Commands\Cmd*.cs`, UI (`BibimcadDialog`, `CadLinkPicker`, `WallReviewWindow`, `HelpWindow`), Revit-side services (`CadLinkLocator`, `CoordinateTransformService`, `WallGenerationService`, `WallValidationService`, `LinkGeometryReader`). |
| `tests\Bibimcad.Core.Tests` | xUnit, `net48`. Runs without Revit. |
| `tests\data` | `sample_walls.dxf` and the script that generates it. |
| `installer\Bibimcad.addin` | Manifest template (`FULL_DLL_PATH_PLACEHOLDER`). |
| `installer\Check-Bibimcad.ps1` | Install diagnostic; writes `Bibimcad_check.txt` to the Desktop. |
| `data\project_state.json` | Template only. The add-in reads `C:\Bibimcad\data\project_state.json`. |

## Build and test

```powershell
dotnet build .\Bibimcad.sln -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
dotnet test tests\Bibimcad.Core.Tests\Bibimcad.Core.Tests.csproj
```

- Close Revit before building; it locks `Bibimcad.Addin.dll`.
- Output: `src\Bibimcad.Addin\bin\Debug\` with `Bibimcad.Addin.dll`, `Bibimcad.Core.dll`, `Newtonsoft.Json.dll`, `netDxf.dll`. They must stay together.
- Do not use `--no-restore` after pulling; new packages may have been added.
- A build proves nothing about Revit. Only restarting Revit and clicking the buttons does.

## Rules for code changes

- Stay on `net48` and C# 7.3. Revit 2023 cannot load newer .NET. `ElementId` is `int`-based in 2023 (`new ElementId(int)`, `IntegerValue`).
- Commands call services. Services never show dialogs; UI classes do.
- `JsonProjectRepository` is the only JSON serializer. Do not create a second state file.
- Never invent data. Values a human decides (`unitCalibrationApproved`, `mmPerCadUnit`, `wallLayers`, review status, heights) are never changed by code or by an agent without the user.
- Put logic that does not need Revit in `Bibimcad.Core` and add tests for it.
- New JSON properties must be optional so older files still load. Bump `schemaVersion` only for breaking changes, and extend `ProjectState.UpgradeToCurrentSchema()`.
- New ribbon buttons go in `RibbonBuilder.Buttons` (`ButtonSpec`).
- Update `readme.md`, `handbook.md`, `docs\architecture.md`, and this file when behavior changes.
- Do not commit unless the user asks. `bin\` and `obj\` are ignored.

## Rules for working on this PC

- Do not put any `.addin` file under `C:\Program Files\Autodesk\Revit 2023\`. See the history below.
- Never overwrite `C:\Bibimcad\data\project_state.json`. Back it up before editing (`C:\Bibimcad\backup\`).
- Never close or kill Revit; ask the user to save and close it.
- Changes under `Program Files` need an administrator PowerShell. Rename (`.disabled`), do not delete.
- The user is not a programmer. Give short numbered steps with exact clicks, and wait for "done" between Revit steps. Thai is fine.
- Separate what you observed (file paths, line numbers, dialog text) from what you suspect.

## Current state (2026-10-15)

Working on the coworker's PC:
- Install loads; the Bibimcad tab shows.
- K1 DXF read after exploding the drawing (it was one block). Wall layer is `HV-WALL` (4720 entities).
- 04 Classify, 05 Review, and 06 Generate work. Generated walls have the right size (for example 4800 x 212 mm).

Open problem: **generated walls land away from the CAD drawing**, as a small cluster near the origin.
- 02 Coordinates reported origin 0/0/0, rotation 0, scale 1, 6000 mm check passed.
- Main suspect: K1.dwg was linked with Import units = Auto-Detect, read the `Inches` header, and Revit draws the CAD 25.4 times too big. The 6000 mm check cannot see this.
- The coworker has been snapping walls into place by hand. That makes 07 Validate fail and must be redone each time.

New since then, **not yet run inside Revit**:
- 02 Coordinates adds a "Link size check" that compares the line work on `wallLayers` in the DXF with the same layers in the Revit link. It saves `revitLink.sizeCheck` with `result` = `Passed`, `ScaleMismatch` (with `likelyImportUnit`), `Offset`, `Inconclusive`, or `Skipped`.
- 06 Generate does not refuse to run; it shows a warning in its confirmation when the size check failed.
- 08 Help opens a window listing problems found in `project_state.json` with numbered fixes, plus common problems.

## Next steps

1. Upgrade the install (handbook, Upgrade section), restart Revit, confirm buttons 01 to 08.
2. Ask the user to click **02 Coordinates** and send the "Link size check" lines.
   - `ScaleMismatch`, ratio about 25.4: follow the relink steps in 08 Help (Insert > Link CAD, Import units = Millimeter, Auto - Origin to Origin). Delete the Bibimcad walls first, then 02, 06, 07.
   - `Offset`: the DXF came from a moved or different copy; ask for a new DXF from the linked K1.dwg.
   - `Passed` but walls still misplaced: report the evidence; the size check has a gap.
   - `Inconclusive` with "No line work was found in the Revit CAD link": `LinkGeometryReader` probably did not match the layer name through `GraphicsStyleCategory.Name`. Retry once with `wallLayers` temporarily empty (all layers) to tell whether layer matching is the cause, then restore it. Report before changing code.
3. After a correct relink, 07 Validate should pass. Record the outcome in the history below.
4. Warn the user that grids and columns placed on the oversized CAD will no longer match it.

Possible later work (only when the user asks): read DWG directly (ACadSharp) or from the Revit link geometry; a per-layer "looks like walls" score in 03 Read DXF; doors and windows are out of scope for now.

## History

### 2026-10-14: ribbon tab missing after install (fixed)

- Symptom: `dotnet build` succeeded, but Revit 2023 showed no Bibimcad tab.
- Evidence: the journal reported `The addin file -Bibimcad.addin- in current user folder is duplicated.` and `The assembly -Bibimcad.addin- in internal addin is not signed as internal addin.` A second manifest with `FULL_DLL_PATH_PLACEHOLDER` existed at `C:\Program Files\Autodesk\Revit 2023\AddIns\2023\Bibimcad.addin`.
- Fix: renamed it to `Bibimcad.addin.disabled` from an elevated PowerShell. Keep that backup. The active manifest is `%AppData%\Autodesk\Revit\Addins\2023\Bibimcad.addin` with `<Assembly>C:\bibimcad-builder-main\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll</Assembly>`.
- `Check-Bibimcad.ps1` now also scans the Revit `AddIns` folder for this case.
- Sandboxed agents may see a fake `LOCALAPPDATA`; the real journals are in `C:\Users\<user>\AppData\Local\Autodesk\Revit\Autodesk Revit 2023\Journals`.

### 2026-10-14: 03 Read DXF found only `INSERT: 1`

- The whole K1 drawing was one block in model space, so no wall lines were visible to Bibimcad.
- Fix: exploded a copy of K1.dwg in AutoCAD and saved it as `C:\Bibimcad\input\K1.dxf`. Layers then appeared; walls are on `HV-WALL`.

### 2026-10-14: walls generated away from the CAD (open)

See "Current state" and "Next steps".
