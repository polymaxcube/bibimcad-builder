# Bibimcad install handbook

Follow this file to install the Bibimcad Revit 2023 add-in on a Windows machine. `readme.md` is the short version for a person. This file is the procedure.

## Goal

Produce `Bibimcad.Addin.dll`, point a `.addin` manifest at that DLL, put the manifest where Revit 2023 scans, and copy `project_state.json` to the path the add-in reads.

Revit loads the add-in. There is no `dotnet run`.

## Do not change

- Do not retarget `src\Bibimcad.Addin\Bibimcad.Addin.csproj` away from `net48`. Revit 2023 cannot load a .NET 8 DLL.
- Do not change `<FullClassName>Bibimcad.App</FullClassName>`.
- Do not change `<AddInId>6f4c2a81-9e37-4b5d-8c14-2a7e9b0d4f63</AddInId>`.
- Do not copy `RevitAPI.dll` or `RevitAPIUI.dll` into the build output or into the Addins folder. The project deletes those copies after build. Revit already has them.
- Do not put only the file name `Bibimcad.Addin.dll` in `<Assembly>`. Revit needs the full path.
- Do not commit a machine-specific DLL path inside `installer\Bibimcad.addin` unless the user asks. Edit the deployed copy.

## 1. Check the machine

Run in PowerShell:

```powershell
dotnet --version
Test-Path "C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll"
Test-Path "C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll"
```

Required results:

- `dotnet --version` prints a version. Any installed SDK can build `net48`.
- Both `Test-Path` commands print `True`, or you find another folder that contains both DLLs. Call that folder `RevitInstallDir`.

If `dotnet` is missing, stop and tell the user to install the .NET SDK. If the Revit 2023 DLLs are missing, stop. This add-in cannot be built or loaded without Revit 2023.

The build also needs the .NET Framework 4.8 targeting pack. If `dotnet build` later reports that `net48` is not installed, stop and tell the user to install the .NET Framework 4.8 Developer Pack.

## 2. Get the source

Repository: `https://github.com/polymaxcube/bibimcad-builder.git`

If this handbook is already inside a checkout, use that folder. Otherwise:

```powershell
git clone https://github.com/polymaxcube/bibimcad-builder.git
cd bibimcad-builder
```

The working directory for every later command is the folder that contains `Bibimcad.sln`.

Confirm these files exist:

- `Bibimcad.sln`
- `src\Bibimcad.Addin\Bibimcad.Addin.csproj`
- `installer\Bibimcad.addin`
- `data\project_state.json`

## 3. Build

Default install folder:

```powershell
dotnet build -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
```

Other install folder:

```powershell
dotnet build -p:RevitInstallDir="D:\Autodesk\Revit 2023"
```

`dotnet build` with no `-c` flag writes Debug output. Success looks like `Build succeeded` and exit code 0.

Confirm both files exist:

- `src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll`
- `src\Bibimcad.Addin\bin\Debug\Newtonsoft.Json.dll`

Leave both files in that folder. `Newtonsoft.Json.dll` must stay beside `Bibimcad.Addin.dll`, because Revit loads dependencies from the DLL's folder.

A Release build uses `src\Bibimcad.Addin\bin\Release\` instead. Use one configuration and point the manifest at that same folder.

## 4. Write the manifest Revit will read

Read `installer\Bibimcad.addin`. It contains `<Assembly>FULL_DLL_PATH_PLACEHOLDER</Assembly>`.

Create the Addins folder if needed, then copy the manifest there:

```powershell
$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2023"
New-Item -ItemType Directory -Force -Path $addins
Copy-Item .\installer\Bibimcad.addin (Join-Path $addins "Bibimcad.addin")
```

Set `<Assembly>` in the copied file to the full path of the DLL from step 3. Example:

```xml
<Assembly>C:\src\bibimcad-builder\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll</Assembly>
```

Leave every other element as it is in `installer\Bibimcad.addin`.

Revit scans `%AppData%\Autodesk\Revit\Addins\2023\` for files whose name ends in `.addin`. The copy's file name may stay `Bibimcad.addin`. Revit does not treat the `.Addin` segment of the DLL name as special.

## 5. Install the project state file

The add-in reads only `C:\Bibimcad\data\project_state.json`. It does not create this file when it is missing. `PathUtils.GetProjectRoot()` returns `C:\Bibimcad`.

```powershell
New-Item -ItemType Directory -Force -Path C:\Bibimcad\data
Copy-Item .\data\project_state.json C:\Bibimcad\data\project_state.json
```

Do not point the add-in at `data\project_state.json` inside the repository. That file is the template.

The log folder does not need to be created. The add-in creates `C:\Bibimcad\logs\` and appends `Bibimcad.log` on startup.

## 6. Verify the installed files

All of these must be true before telling the user to open Revit:

- `%AppData%\Autodesk\Revit\Addins\2023\Bibimcad.addin` exists.
- Its `<Assembly>` value is a full path, and that path exists.
- `Newtonsoft.Json.dll` exists in the same folder as that DLL.
- `<FullClassName>` is `Bibimcad.App`.
- `C:\Bibimcad\data\project_state.json` exists and is not empty.
- `installer\Bibimcad.addin` in the repository still says `FULL_DLL_PATH_PLACEHOLDER`, unless the user asked to store a machine path in git.

PowerShell checks:

```powershell
$manifest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2023\Bibimcad.addin"
[xml]$xml = Get-Content $manifest
$dll = $xml.RevitAddIns.AddIn.Assembly
Test-Path $dll
Test-Path (Join-Path (Split-Path $dll) "Newtonsoft.Json.dll")
Test-Path C:\Bibimcad\data\project_state.json
$xml.RevitAddIns.AddIn.FullClassName
```

Expected class name: `Bibimcad.App`. Both `Test-Path` results for the DLL and `Newtonsoft.Json.dll` must be `True`.

## 7. What the user does in Revit

Ask the user to start Revit 2023, or to restart it if it was already open. Revit reads add-ins only at startup.

On startup it loads `Bibimcad.App` from `App.cs`. That creates:

- Tab: **Bibimcad**
- Panel: **K1 Workflow**
- Button **01 Inspect**: reads `C:\Bibimcad\data\project_state.json`
- Button **02 Coordinates**: reads the linked CAD transform and saves it into that same file

If the tab is missing, read `C:\Bibimcad\logs\Bibimcad.log`. A startup failure is also reported by a Revit task dialog when the log itself cannot be written.

## Rebuild

After source changes:

```powershell
dotnet build -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
```

Use the same `RevitInstallDir` as the first build. If the DLL path did not change, leave the manifest in the Addins folder alone. Tell the user to restart Revit. Revit keeps the old DLL loaded until it exits.
