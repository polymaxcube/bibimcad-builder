# Bibimcad

Revit 2023 add-in. Build it with the .NET CLI. Revit loads it. No code editor is required to install it.

## What you need

1. [Revit 2023](https://www.autodesk.com/products/revit/).
2. The [.NET SDK](https://dotnet.microsoft.com/download). Any current SDK can build this project. Check with `dotnet --version`.
3. The [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48). The project targets `net48` because Revit 2023 only loads that. Do not retarget it to .NET 8.

## 1. Get the project

```powershell
git clone https://github.com/polymaxcube/bibimcad-builder.git
cd bibimcad-builder
```

## 2. Build the DLL

From the folder that contains `Bibimcad.sln`:

```powershell
dotnet build -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
```

If Revit is installed somewhere else, pass the folder that contains `RevitAPI.dll` and `RevitAPIUI.dll`.

A successful build writes:

`src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll`

`Bibimcad.Core.dll`, `Newtonsoft.Json.dll`, and `netDxf.dll` are written next to it. Leave all of them in that folder; Revit loads them from there.

## 3. Point the manifest at that DLL

Open `installer\Bibimcad.addin`. Replace `FULL_DLL_PATH_PLACEHOLDER` with the full path of the DLL from step 2:

```xml
<Assembly>C:\path\to\bibimcad-builder\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll</Assembly>
```

Use the real path on this machine. The file name alone is not enough.

## 4. Copy the manifest where Revit looks

Copy the edited `Bibimcad.addin` to:

`%AppData%\Autodesk\Revit\Addins\2023\`

In PowerShell that folder is:

```powershell
$env:APPDATA\Autodesk\Revit\Addins\2023
```

Revit reads only the copy in that folder. It recognizes the `.addin` extension. The `.Addin` part of the DLL name is the C# project name and does not tell Revit anything.

## 5. Copy the project data file

The add-in reads and writes `C:\Bibimcad\data\project_state.json`. Copy the template before the first command:

```powershell
New-Item -ItemType Directory -Force -Path C:\Bibimcad\data
Copy-Item .\data\project_state.json C:\Bibimcad\data\project_state.json
```

Logs are written to `C:\Bibimcad\logs\Bibimcad.log`.

## 6. Start Revit 2023

On startup Revit loads `Bibimcad.App` from `App.cs`. That creates a **Bibimcad** tab with a **K1 Workflow** panel and eight buttons:

- **01 Inspect** reads the project state.
- **02 Coordinates** reads the linked CAD transform and saves it. It also checks that K1.dwg in Revit has the same size and position as the DXF, and says so when the link was imported in the wrong unit (for example 25.4 times too big from inches).
- **03 Read DXF** reads `cadSource.dxfPath` and lists its layers and entity counts.
- **04 Classify** finds wall candidates on the wall layers.
- **05 Review** opens a window to approve or reject candidates and set their heights.
- **06 Generate** creates Revit walls for the approved candidates.
- **07 Validate** checks those walls against the drawing within 1 mm.
- **08 Help** shows what is wrong in this project right now and how to fix it, step by step, plus answers to common problems.

There is no `dotnet run`. Revit is the program that runs the add-in.

## 7. Turn the DXF into walls

Open the Revit project with `K1.dwg` linked, then:

1. Save `K1.dwg` as DXF (AutoCAD 2000 or later) at the path in `cadSource.dxfPath`, by default `C:\Bibimcad\input\K1.dxf`. DWG files cannot be read.
2. Click **03 Read DXF**. Note which layers hold the walls.
3. In `C:\Bibimcad\data\project_state.json`, put those layer names in `cadSource.wallLayers`. Optionally set `project.defaultWallHeightMm` (for example `3000`) and `project.targetLevelName` (for example `"Level 1"`):

   ```json
   "wallLayers": ["A-WALL"],
   ```

4. Click **04 Classify**. Every candidate starts as unverified.
5. Click **05 Review**. Approve the real walls, give each a height, and click **Save**. **Export CSV** and **Import CSV** let you review in Excel instead (`C:\Bibimcad\data\wall_review.csv`).
6. Click **06 Generate** and confirm. Clicking it again does not duplicate walls.
7. Click **07 Validate**. Failures are listed and saved under `validation` in the JSON file.

Running **04 Classify** again keeps the review of candidates whose geometry did not change.

If walls land away from the CAD drawing, click **02 Coordinates** and read its link size check, then **08 Help**. The usual cause is K1.dwg linked with **Import units: Auto-Detect**, which Revit reads as inches. Relink it with **Import units: Millimeter** instead of moving walls by hand.

## After you change the code

Run `dotnet build` again, then restart Revit. Revit keeps the DLL loaded until it closes.

The DXF reading and wall logic lives in `src\Bibimcad.Core` and has tests that run without Revit:

```powershell
dotnet test tests\Bibimcad.Core.Tests\Bibimcad.Core.Tests.csproj
```

See `docs\architecture.md` for how the pieces fit together.
