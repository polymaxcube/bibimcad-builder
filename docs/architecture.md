# Bibimcad architecture

Bibimcad is a native Autodesk Revit 2023 add-in. It reads a K1 DXF, finds wall candidates, lets a person review them, creates Revit walls for the approved ones, and checks that those walls match the drawing. Doors, windows, floors, and everything else are out of scope.

## Runtime flow

```
CAD / Revit link
        ↓
Revit command
        ↓
Service
        ↓
JsonProjectRepository
        ↓
C:\Bibimcad\data\project_state.json
```

Every step reads and writes the same file:

```
DXF
 ↓
03 Read DXF     → cadAudit
 ↓
04 Classify     → wallClassification, wallCandidates
 ↓
05 Review       → wallCandidates[].status / heightMm / notes
 ↓
06 Generate     → Revit walls, wallCandidates[].revitElementId
 ↓
07 Validate     → validation
```

`01 Inspect` reads the file. `02 Coordinates` saves `revitLink`, including the link size check in `revitLink.sizeCheck`. `08 Help` only reads the file and explains what to do next.

## Projects

```
Bibimcad.Addin (Revit add-in)          Bibimcad.Core (no Revit references)
  App, RibbonBuilder                     Models
  Commands                    ───→       JsonProjectRepository
  UI (dialogs, WPF review)               DXF reading and audit (netDxf)
  Services that touch Revit API          Wall classification, merge, review, CSV
                                         Geometry comparison for validation
                                         PathUtils, LoggingUtils
```

`Bibimcad.Core` holds everything that can run without Revit, so `tests\Bibimcad.Core.Tests` (xUnit, `net48`) runs with `dotnet test` on any machine. `tests\data\sample_walls.dxf` is a small drawing with known walls. `tests\data\New-SampleWallsDxf.ps1` regenerates it.

`App` only starts and stops the ribbon. `RibbonBuilder` only creates the Bibimcad tab. Commands call services. Services do not show dialogs; `UI\BibimcadDialog`, `UI\CadLinkPicker`, and `UI\WallReviewWindow` do. `JsonProjectRepository` is the only JSON serializer.

## Why the JSON file is central

`project_state.json` is the project memory. Every command loads it, changes its own section, and saves the same object. No command writes a second state file. `wall_review.csv` is an exchange file for Excel; its content only counts once it is imported back.

The repository rewrites the whole `ProjectState`. A value survives only when it has a property on the model. Missing files are not replaced with invented data.

The source repository contains a template at `data/project_state.json`. The running add-in does not read that template. `PathUtils` points at the runtime root:

- `C:\Bibimcad\data\project_state.json`
- `C:\Bibimcad\data\wall_review.csv`
- `C:\Bibimcad\logs\Bibimcad.log`

Copy the template into `C:\Bibimcad\data` before the first command.

### Schema

The current `schemaVersion` is `1.1`. It adds:

- `project.targetLevelName` and `project.defaultWallHeightMm`
- `cadSource.wallLayers`
- `cadAudit.sourceFile`, `dxfVersion`, `readAt`, and `layers`
- `wallClassification`, `wallCandidates`, and `validation`

A `1.0` file still loads. Commands that write call `ProjectState.UpgradeToCurrentSchema()`, which fills the new lists with empty values and saves the file as `1.1`. Other versions are rejected.

Values a person sets by hand in the JSON file:

| Property | Used by |
| --- | --- |
| `cadSource.wallLayers` | 04 Classify. Only these layers are read. |
| `project.defaultWallHeightMm` | 05 Review. The height used when a row with no height is approved. |
| `project.targetLevelName` | 06 Generate. Falls back to the active view's level. |

## DXF reading

`netDxf` 2023.11.10 (MIT, `net48`, no dependencies) reads the DXF. It reads AutoCAD 2000 and later DXF files. It does not read DWG or R12, so `K1.dwg` must be saved as DXF first. `DxfLoader` refuses those files with a message.

`03 Read DXF` fills `cadAudit`: layer and block counts, entity counts by type, a per-layer entity count, the DXF version, and the source file. It copies the `INSUNITS` header into `cadSource.headerInsUnits` and warns when it disagrees with `approvedCoordinateUnit`. The human approval still wins.

## Wall classification

`04 Classify` refuses to run without `dxfPath`, `unitCalibrationApproved`, a positive `mmPerCadUnit`, and at least one name in `wallLayers`.

1. `LINE` and straight `LWPOLYLINE` segments on the wall layers become segments in millimeters (`mmPerCadUnit`). Arc segments are counted and skipped.
2. Two segments pair when they are parallel within 1 degree, 75 to 350 mm apart, and overlap by at least 70% of the shorter one. Each segment is used once; the closest, most overlapping pairs win.
3. Each pair becomes a candidate: the centerline is the midline of the overlap, the thickness is the gap, and the id is `W-` plus both source segment ids. The orientation is horizontal or vertical within 1 degree, otherwise angled.
4. Unpaired segments are counted and reported, not turned into candidates.

Every candidate starts as `Unverified`. Nothing is approved automatically.

Re-running Classify merges with the previous candidates. A candidate with the same geometry keeps its status, height, notes, and Revit wall id. A candidate whose geometry changed is reset to `Unverified` with a note. The report says how many review decisions were dropped.

## Review

`05 Review` opens a modal WPF window over Revit. It filters by layer and status, approves or rejects several rows at once, sets heights, and saves. A row cannot be approved without a height; `defaultWallHeightMm` fills it when set. Cancel discards everything.

The window can export and import `wall_review.csv` with the columns of the earlier Python tool. Import checks each row completely before applying it. Unknown ids and bad values are listed in the report.

## Coordinate flow

Approved CAD coordinates are millimeters. The sample calibration is `AutoCAD DIST 6000 = drawing dimension 6000 mm`. DXF `INSUNITS` may say inches. Bibimcad does not trust that header by itself. `cadSource.approvedCoordinateUnit` and `unitCalibrationApproved` are the human decision.

```
CAD coordinate in mm
        ↓
MillimetersToFeet
        ↓
CAD local XYZ
        ↓
ImportInstance.GetTotalTransform()
        ↓
Transform.OfPoint()
        ↓
Revit model coordinate
```

`CoordinateTransformService.CadMillimetersToRevitPoint` follows those steps. Generate and Validate both use it with the live link transform. Neither multiplies by a hidden site offset.

JSON stores the transform origin and basis in Revit internal units (feet). The Coordinates dialog converts the origin to millimeters for display only.

`ValidateKnownDistance` maps local points `(0,0,0)` and `(6000,0,0)` through the same path and checks that the resulting distance is about 6000 mm. A difference of 1 mm or less passes.

That check cannot see the import units. When K1.dwg is linked with Import units = Auto-Detect and an `Inches` header, Revit scales the link geometry by 25.4 while `GetTotalTransform()` keeps scale 1. Walls are then the right size but land at 1/25.4 of their distance from the origin.

### Link size check

`02 Coordinates` therefore also compares the line work on `cadSource.wallLayers` (all layers when empty):

- DXF side: `DxfExtentsReader` collects lines and polyline vertices in millimeters, including lines inside block references.
- Revit side: `LinkGeometryReader` walks the link geometry (including nested blocks), maps every point to model coordinates, and back through the inverse of `GetTotalTransform()`. The result is in the same CAD-local millimeters that Generate feeds forward.
- `LinkSizeCheck.Evaluate` compares the two boxes. A ratio within 1% of 1 and a shift of 5 mm or less passes. A ratio that matches 25.4, 304.8, 10, 100, or 1000 names the likely import unit. Matching size with a larger shift reports `Offset`.

The result is saved as `revitLink.sizeCheck`. Generate does not refuse to run when it failed; its confirmation shows a warning. Fix the link, not `mmPerCadUnit`.

## Why the transform is not hardcoded

The placement of K1 can change. Coordinates, Generate, and Validate call `ImportInstance.GetTotalTransform()` on the current link every time. Previous observed origin values are not constants in this code. If the link moves after walls are generated, Validate reports the endpoint error.

Scale, rotation, and mirror flags are derived from that same transform:

- scale is the length of each basis vector
- rotation is the Z-axis angle of basis X, in degrees
- mirrored is `transform.Determinant < 0`

## How the CAD link is chosen

`CadLinkLocator` (service) finds candidates and `CadLinkPicker` (UI) asks when needed. Coordinates, Generate, and Validate share them.

1. Collect `ImportInstance` elements in the active document whose `IsLinked` is true.
2. Resolve the CAD type name, with a parameter fallback if `Element.Name` is unavailable.
3. Keep instances whose type or file name matches `cadSource.dwgName` (`K1.dwg` for the sample).
4. If exactly one match exists, use it.
5. If two to four matches exist, ask for one in a list.
6. If more than four matches exist, or none exist, ask the user to pick an `ImportInstance`.

## Wall generation

`06 Generate` handles approved candidates only. It asks for confirmation before it changes the model.

- Level: `project.targetLevelName`, otherwise the active view's level. The command stops if neither exists. Wall endpoints are placed at that level's elevation.
- Wall type: a basic wall type named `Bibimcad - {thickness rounded to mm} mm`. It is reused when it exists, otherwise duplicated from a basic wall type (a "Generic" one when possible) with one structural layer of that width.
- Height: the candidate's `heightMm`.
- All walls are created in one transaction named `Bibimcad - Generate walls`. Revit warnings are suppressed and counted. A failure on one candidate is reported and does not stop the others.
- Each new wall id is stored in `revitElementId` after the transaction commits. A rerun skips candidates whose wall still exists, and recreates the ones whose wall was deleted, so walls are never duplicated.

## Validation

`07 Validate` is read-only. For each approved candidate it checks:

- the stored `revitElementId` exists and is a wall
- both location-line endpoints sit within 1 mm of the mapped candidate endpoints, in either direction
- the location-line length matches the candidate length within 1 mm
- the wall width matches the candidate thickness within 1 mm
- the unconnected height matches `heightMm` within 1 mm

It writes `validation` (timestamp, tolerance, checked, passed, and failed counts, and one failure entry per wall with every reason) and shows a summary. Wall type widths are rounded to whole millimeters, so a generated wall differs from its candidate thickness by at most 0.5 mm and passes.

## Command boundaries

| Command | Transaction | Changes the Revit model |
| --- | --- | --- |
| 01 Inspect | Read-only | No |
| 02 Coordinates | Read-only | No |
| 03 Read DXF | Read-only | No |
| 04 Classify | Read-only | No |
| 05 Review | Read-only | No |
| 06 Generate | Manual, one transaction | Yes: walls and `Bibimcad - … mm` wall types |
| 07 Validate | Read-only | No |
| 08 Help | Read-only, does not save | No |

`08 Help` builds its list with `HelpAdvisor` in Core from `project_state.json`: missing files, a DXF that is one block, empty or misspelled wall layers, a failed link size check, approved walls without height, walls not generated, and validation failures. Each topic has numbered steps. A fixed list of common problems follows.

No AI classification, doors, windows, floors, stairs, equipment, structure, MEP, or Revit 2026 support is included.

## Build boundary

`Bibimcad.Addin` is a .NET Framework 4.8 class library (`net48`) with WPF enabled. It references `RevitAPI.dll` and `RevitAPIUI.dll` from the Revit 2023 install (`RevitInstallDir`) with Copy Local set to false. Those assemblies are not stored in this repository. A computer without Revit 2023 cannot compile or load the add-in, but it can build and test `Bibimcad.Core`.

Four DLLs are deployed together in the add-in output folder, and Revit loads them from there:

- `Bibimcad.Addin.dll`
- `Bibimcad.Core.dll`
- `Newtonsoft.Json.dll` 13.0.3
- `netDxf.dll` 2023.11.10

The `.addin` manifest is not copied into the Revit Addins folder by the build.
