# How Bibimcad works

## The workflow in Revit

```mermaid
flowchart TD
    start([Open the Revit project with K1.dwg linked]) --> inspect["01 Inspect<br/>read project_state.json"]
    inspect --> stateOk{File readable?}
    stateOk -- No --> help1["08 Help<br/>copy the template or fix the JSON"] --> inspect
    stateOk -- Yes --> coords["02 Coordinates<br/>save the link position<br/>and run the link size check"]

    coords --> sizeOk{Size check<br/>Passed?}
    sizeOk -- "ScaleMismatch<br/>(e.g. 25.4x, inches)" --> relink["Relink K1.dwg<br/>Import units = Millimeter"] --> coords
    sizeOk -- Offset --> newDxf["Save a new DXF from<br/>the linked K1.dwg"] --> coords
    sizeOk -- Yes --> dxfReady

    dxfReady{"K1.dxf ready?<br/>(DXF, not DWG)"} -- No --> saveDxf["AutoCAD: Save As DXF<br/>to cadSource.dxfPath"] --> dxfReady
    dxfReady -- Yes --> read["03 Read DXF<br/>layers and entity counts"]
    read --> block{"Only INSERT: 1?<br/>(drawing is one block)"}
    block -- Yes --> explode["AutoCAD: EXPLODE or BURST<br/>save the DXF again"] --> read
    block -- No --> layers["Find the wall layer<br/>(Revit: Query on a wall line)<br/>set cadSource.wallLayers"]

    layers --> classify["04 Classify<br/>pair parallel lines 75-350 mm apart<br/>into wall candidates"]
    classify --> found{Candidates found?}
    found -- No --> checkLayers["Check wallLayers and units<br/>(08 Help)"] --> layers
    found -- Yes --> review["05 Review<br/>approve real walls, set heights, Save<br/>(or Excel via CSV)"]

    review --> generate["06 Generate<br/>confirm, then create walls<br/>on the target level"]
    generate --> validate["07 Validate<br/>endpoints, length, width, height<br/>within 1 mm"]
    validate --> passed{All walls passed?}
    passed -- No --> help2["08 Help<br/>fix the cause, delete failing walls"] --> generate
    passed -- Yes --> done([3D walls in Revit])
```

**08 Help** can be clicked at any point. It reads `project_state.json` and lists the problems it finds, with numbered steps.

Rerunning is safe:
- **04 Classify** keeps the review of walls whose geometry did not change.
- **06 Generate** never duplicates walls. It skips existing walls and recreates deleted ones.

## What each button reads and writes

Every button goes through the same layers and the same file.

```mermaid
flowchart LR
    subgraph revit [Revit 2023]
        ribbon["Bibimcad tab<br/>01 to 08 buttons"]
        link["K1.dwg link"]
        walls["Revit walls"]
    end

    subgraph addin [Bibimcad.Addin]
        commands["Commands<br/>CmdReadDxf, CmdGenerateWalls, ..."]
        ui["UI<br/>dialogs, Review window, Help window"]
        revitServices["Revit services<br/>link locator, link geometry,<br/>wall generation, validation"]
    end

    subgraph core [Bibimcad.Core - no Revit]
        coreServices["DXF reader, wall classifier,<br/>review, CSV, size check, help advisor"]
        repo["JsonProjectRepository"]
    end

    dxf[("K1.dxf")]
    state[("C:\Bibimcad\data\<br/>project_state.json")]

    ribbon --> commands
    commands --> ui
    commands --> revitServices
    commands --> coreServices
    revitServices --> link
    revitServices --> walls
    coreServices --> dxf
    commands --> repo
    repo <--> state
```

| Button | Reads | Writes in `project_state.json` | Changes the Revit model |
| --- | --- | --- | --- |
| 01 Inspect | project_state.json | nothing | No |
| 02 Coordinates | K1.dwg link, K1.dxf | `revitLink` (position and `sizeCheck`) | No |
| 03 Read DXF | K1.dxf | `cadAudit`, `cadSource.headerInsUnits` | No |
| 04 Classify | K1.dxf, `wallLayers` | `wallClassification`, `wallCandidates` | No |
| 05 Review | `wallCandidates` | status, height, notes of candidates | No |
| 06 Generate | approved candidates, K1.dwg link | `revitElementId` of each wall | Yes: walls and wall types |
| 07 Validate | approved candidates, Revit walls | `validation` | No |
| 08 Help | project_state.json | nothing | No |

## How a CAD point becomes a Revit point

```mermaid
flowchart LR
    cad["DXF point<br/>(CAD units)"] -->|"x mmPerCadUnit"| mm["CAD point in mm"]
    mm -->|"mm to feet"| local["CAD-local point<br/>(Revit units)"]
    local -->|"K1.dwg link transform<br/>GetTotalTransform()"| model["Revit model point"]
```

The link transform is read from the live K1.dwg link every time, so moving the link moves where new walls go. The link size check in **02 Coordinates** confirms that Revit draws the link at the same size as this path assumes.
