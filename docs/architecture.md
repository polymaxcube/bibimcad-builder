# Bibimcad architecture

Bibimcad is a native Autodesk Revit 2023 add-in. Phase 1 proves that the add-in can load, expose a ribbon, and read or write one central project file. It does not create BIM geometry.

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

Later phases keep the same file and extend the model:

```
DXF
 ↓
Inspect
 ↓
Central JSON
 ↓
Classify
 ↓
Review
 ↓
Approve
 ↓
Generate Revit BIM
 ↓
Validate
```

Code is arranged to match that flow:

```
Bibimcad Revit Add-in
        ↓
Commands
        ↓
Services
        ↓
Models
        ↓
JsonProjectRepository
        ↓
project_state.json
```

`App` only starts and stops the ribbon. `RibbonBuilder` only creates the Bibimcad tab. Commands call services. Services do not show dialogs. `JsonProjectRepository` is the only JSON serializer.

## Why the JSON file is central

`project_state.json` is the project memory. Inspect reads it. Coordinates updates `revitLink` inside it and saves the same object. Classification and generation, when they exist, must read and write this model rather than a second file.

The repository rewrites the whole `ProjectState`. A value survives only when it has a property on the model. Missing files are not replaced with invented data.

The source repository contains a template at `data/project_state.json`. The running add-in does not read that template. `PathUtils` points at the Phase 1 runtime root:

- `C:\Bibimcad\data\project_state.json`
- `C:\Bibimcad\logs\Bibimcad.log`

Copy the template into `C:\Bibimcad\data` before the first Inspect command. A later phase can replace `PathUtils.GetProjectRoot()` with configuration.

## Coordinate flow

Approved CAD coordinates are millimeters. The sample calibration is `AutoCAD DIST 6000 = drawing dimension 6000 mm`. DXF `INSUNITS` may say inches. Bibimcad does not trust that header by itself. `cadSource.approvedCoordinateUnit` and `unitCalibrationApproved` are the human decision. Phase 1 stores that decision and does not parse DXF.

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

`CoordinateTransformService.CadMillimetersToRevitPoint` follows those steps. The caller passes millimeters. The service converts them with `UnitUtils` and then applies the live link transform. It does not multiply by a hidden site offset.

JSON stores the transform origin and basis in Revit internal units (feet). The Coordinates dialog converts the origin to millimeters for display only.

`ValidateKnownDistance` maps local points `(0,0,0)` and `(6000,0,0)` through the same path and checks that the resulting distance is about 6000 mm. The check is about distance, not about where the link sits in the model. A difference of 1 mm or less passes. The check does not throw for that tolerance.

## Why the transform is not hardcoded

The placement of K1 can change. `CmdCheckCoordinates` calls `ImportInstance.GetTotalTransform()` on the current link and saves that result. Previous observed origin values are not constants in this code.

Scale, rotation, and mirror flags are derived from that same transform:

- scale is the length of each basis vector
- rotation is the Z-axis angle of basis X, in degrees
- mirrored is `transform.Determinant < 0`

## How the CAD link is chosen

1. Collect `ImportInstance` elements in the active document whose `IsLinked` is true.
2. Resolve the CAD type name, with a parameter fallback if `Element.Name` is unavailable.
3. Keep instances whose type or file name matches `cadSource.dwgName` (`K1.dwg` for the sample).
4. If exactly one match exists, use it.
5. If two to four matches exist, ask for one in a list.
6. If more than four matches exist, or none exist, ask the user to pick an `ImportInstance`.

Manual selection is the fallback. The normal K1 case does not require a click on the link.

## Phase 1 boundary

These commands do not open a Revit transaction and do not create elements:

- `01 Inspect` reads and validates the JSON file.
- `02 Coordinates` reads one CAD transform, saves it, and checks the 6000 mm distance.

No DXF reader, wall classification, AI, WPF review, walls, doors, windows, floors, stairs, equipment, structure, or MEP is included.

## Build boundary

Bibimcad is a .NET Framework 4.8 class library (`net48`). It references `RevitAPI.dll` and `RevitAPIUI.dll` from the Revit 2023 install with Copy Local set to false. Those assemblies are not stored in this repository. A computer without Revit 2023 cannot compile or load the add-in.

Newtonsoft.Json 13.0.3 is copied beside `Bibimcad.Addin.dll` because Revit does not provide it. The `.addin` manifest is not copied into the Revit Addins folder by the build.
