# Bibimcad install handbook

This file is the procedure for an AI agent that installs or upgrades the Bibimcad Revit 2023 add-in on a Windows machine. `AGENTS.md` has the current project state, open problems, and next steps; read it before continuing any work. `readme.md` is the short version for a person. `docs\architecture.md` explains the code.

Follow the steps in order. Run every command in PowerShell. Stop and ask the user whenever a step says so; do not improvise around a failed check.

## Goal

1. Build `Bibimcad.Addin.dll` and its three dependency DLLs.
2. Make exactly one `.addin` manifest, in the user Addins folder, point at that DLL.
3. Make sure `C:\Bibimcad\data\project_state.json` exists, without overwriting a real one.
4. Ask the user to restart Revit 2023 and confirm the **Bibimcad** tab shows buttons 01 to 07.

Revit loads the add-in. There is no `dotnet run`.

## Fixed facts

| Item | Value |
| --- | --- |
| Revit version | 2023 only |
| Target framework | `net48` (C# 7.3) |
| Revit API folder (build only) | `C:\Program Files\Autodesk\Revit 2023` (`RevitInstallDir`) |
| Manifest Revit reads | `%AppData%\Autodesk\Revit\Addins\2023\Bibimcad.addin` |
| Manifest template in the repo | `installer\Bibimcad.addin` (contains `FULL_DLL_PATH_PLACEHOLDER`) |
| Build output | `src\Bibimcad.Addin\bin\Debug\` |
| DLLs that must stay together | `Bibimcad.Addin.dll`, `Bibimcad.Core.dll`, `Newtonsoft.Json.dll`, `netDxf.dll` |
| `FullClassName` | `Bibimcad.App` |
| `AddInId` | `6f4c2a81-9e37-4b5d-8c14-2a7e9b0d4f63` |
| Project data | `C:\Bibimcad\data\project_state.json` |
| Log | `C:\Bibimcad\logs\Bibimcad.log` |
| Revit journals | `C:\Users\<user>\AppData\Local\Autodesk\Revit\Autodesk Revit 2023\Journals` |
| Diagnostic script | `installer\Check-Bibimcad.ps1` |

`RevitInstallDir` is only where the compiler finds `RevitAPI.dll` and `RevitAPIUI.dll`. It is not where the add-in is installed.

## Rules

- Do not retarget any project away from `net48`. Revit 2023 cannot load a .NET 8 DLL.
- Do not change `FullClassName` or `AddInId`.
- Do not copy `RevitAPI.dll` or `RevitAPIUI.dll` anywhere. Revit already has them.
- Put the manifest only in `%AppData%\Autodesk\Revit\Addins\2023\`. **Never put a Bibimcad `.addin` file anywhere under `C:\Program Files\Autodesk\Revit 2023\`.** Revit treats that folder as its own, reports the file as "duplicated" and "not signed as internal addin", and then loads no Bibimcad copy at all.
- There must be exactly one Bibimcad manifest on the machine.
- `<Assembly>` must be a full path to the DLL, not just the file name.
- Do not commit a machine-specific path into `installer\Bibimcad.addin`. Edit the deployed copy.
- Never overwrite an existing `C:\Bibimcad\data\project_state.json`. It holds the user's link transform and wall review. Back it up before any upgrade.
- Do not close or kill Revit yourself. Ask the user to save and close it.
- Changes under `Program Files` need an administrator PowerShell. Rename files there (add `.disabled`); do not delete them, and do not change folder permissions.
- A successful build does not prove Revit loaded the add-in. Only the user seeing the tab does.

## 0. Decide: fresh install or upgrade

```powershell
Test-Path (Join-Path $env:APPDATA "Autodesk\Revit\Addins\2023\Bibimcad.addin")
Test-Path C:\Bibimcad\data\project_state.json
```

- Both `False`: follow **Fresh install** (steps 1 to 6).
- Either `True`: follow **Upgrade** (steps U1 to U6).

## 1. Check the machine

```powershell
dotnet --version
Test-Path "C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll"
Test-Path "C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll"
```

- `dotnet --version` must print a version. Any current SDK builds `net48`. If it is missing, stop and ask the user to install the [.NET SDK](https://dotnet.microsoft.com/download).
- Both `Test-Path` lines must print `True`. If not, ask the user where Revit 2023 is installed and use that folder as `RevitInstallDir` in every build command. If Revit 2023 is not installed, stop.
- If a later build says `net48` reference assemblies are missing, stop and ask the user to install the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48).

## Fresh install

### 2. Get the source

Repository: `https://github.com/polymaxcube/bibimcad-builder.git`

If this handbook is already inside a checkout, use that folder. Otherwise:

```powershell
git clone https://github.com/polymaxcube/bibimcad-builder.git C:\bibimcad-builder-main
cd C:\bibimcad-builder-main
```

Every later command runs in the folder that contains `Bibimcad.sln`. Confirm `Bibimcad.sln`, `src\Bibimcad.Addin\Bibimcad.Addin.csproj`, `installer\Bibimcad.addin`, and `data\project_state.json` exist.

### 3. Build

```powershell
dotnet build .\Bibimcad.sln -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
```

The first build downloads NuGet packages, so it needs internet and must not use `--no-restore`. Success is `Build succeeded` with exit code 0.

Confirm all four DLLs exist in `src\Bibimcad.Addin\bin\Debug\`:

```powershell
"Bibimcad.Addin.dll", "Bibimcad.Core.dll", "Newtonsoft.Json.dll", "netDxf.dll" |
    ForEach-Object { "$_ : " + (Test-Path ".\src\Bibimcad.Addin\bin\Debug\$_") }
```

### 4. Install the manifest

```powershell
$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2023"
New-Item -ItemType Directory -Force -Path $addins | Out-Null
$dll = (Resolve-Path .\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll).Path
(Get-Content .\installer\Bibimcad.addin -Raw).Replace("FULL_DLL_PATH_PLACEHOLDER", $dll) |
    Set-Content (Join-Path $addins "Bibimcad.addin") -Encoding UTF8
```

Leave every other element as it is in the template.

### 5. Install the project data file

Only when `C:\Bibimcad\data\project_state.json` does not exist:

```powershell
New-Item -ItemType Directory -Force -Path C:\Bibimcad\data | Out-Null
if (-not (Test-Path C:\Bibimcad\data\project_state.json)) {
    Copy-Item .\data\project_state.json C:\Bibimcad\data\project_state.json
}
```

The add-in creates `C:\Bibimcad\logs\` itself.

### 6. Verify, then hand over

Go to **Verify** below.

## Upgrade

Use this when Bibimcad was installed before and new code was pushed.

### U1. Ask the user to save and close Revit

Wait until this prints nothing:

```powershell
Get-Process -Name Revit -ErrorAction SilentlyContinue
```

A running Revit locks the DLL, and the build then fails with "file is being used by another process".

### U2. Find the current install and back up

```powershell
$manifest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2023\Bibimcad.addin"
([xml](Get-Content $manifest)).RevitAddIns.AddIn.Assembly
```

The printed path ends in `\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll`. The part before `\src\` is the source folder; call it `$repo` (for example `C:\bibimcad-builder-main`).

```powershell
$repo = "C:\bibimcad-builder-main"   # use the folder found above
$backup = "C:\Bibimcad\backup\$(Get-Date -Format yyyyMMdd-HHmm)"
New-Item -ItemType Directory -Force -Path $backup | Out-Null
Copy-Item C:\Bibimcad\data\project_state.json $backup -ErrorAction SilentlyContinue
Copy-Item "$repo\AGENTS.md", "$repo\SKILLS.md" $backup -ErrorAction SilentlyContinue
```

`AGENTS.md` and `SKILLS.md` are local notes that may not be in the repository.

### U3. Update the source in the same folder

Keep the same folder so the manifest path stays valid.

- If `$repo\.git` exists: `cd $repo; git pull`. If git reports local changes, stop and show them to the user.
  - If git refuses because an untracked `AGENTS.md` would be overwritten, rename the local one first (`Rename-Item AGENTS.md AGENTS.local.md`) and pull again. The repository `AGENTS.md` already contains the confirmed repair history. Compare the two and tell the user about anything in `AGENTS.local.md` that is missing.
- Otherwise (ZIP download): ask the user for the new ZIP, delete the contents of `$repo`, and extract the ZIP so that `$repo\Bibimcad.sln` exists. The ZIP contains `AGENTS.md`; do not overwrite it with the backup. Copy the old one back as `AGENTS.local.md` (and `SKILLS.md` if it was there) and compare as above.

### U4. Build

```powershell
cd $repo
dotnet build .\Bibimcad.sln -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2023"
```

Do not use `--no-restore`; a new version can add packages. Confirm the four DLLs as in step 3.

### U5. Keep the manifest and the data

- If the source folder did not move, do not touch the manifest.
- If it moved, edit only `<Assembly>` in the deployed manifest to the new DLL path.
- Do not copy the template over `C:\Bibimcad\data\project_state.json`. An older `schemaVersion` `1.0` file is upgraded to `1.1` automatically the first time a command saves it.

### U6. Verify, then hand over

Go to **Verify** below.

## Verify

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Check-Bibimcad.ps1
```

It writes `Bibimcad_check.txt` to the Desktop. Every line must be `[PASS]` or `[INFO]`. For each `[FAIL]`, apply the printed fix and run the script again.

The script checks for a Bibimcad manifest under `C:\Program Files\Autodesk\Revit 2023\AddIns`. If it finds one, ask the user to open an administrator PowerShell and run the printed `Rename-Item` command. Then confirm the `.addin` file is gone and the `.addin.disabled` backup exists.

If the agent's environment variables point into a sandbox, `$env:APPDATA` and `$env:LOCALAPPDATA` may not be the real user folders. Use `C:\Users\<user>\AppData\Roaming` and `C:\Users\<user>\AppData\Local` instead before concluding that a file is missing.

## Hand over to the user

Ask the user to start Revit 2023. If Revit shows a security prompt for Bibimcad, choose **Always Load**. Then ask them to confirm:

- Tab **Bibimcad**, panel **K1 Workflow**
- Buttons **01 Inspect**, **02 Coordinates**, **03 Read DXF**, **04 Classify**, **05 Review**, **06 Generate**, **07 Validate**, **08 Help**

Report to the user:

- fresh install or upgrade
- build result
- active manifest path and its `<Assembly>` value
- whether all four DLLs exist
- whether a duplicate manifest was found and disabled
- whether `project_state.json` was kept, created, or backed up (and where)
- whether the user confirmed the tab

## If the tab is missing

1. Run `Check-Bibimcad.ps1` again and fix every `[FAIL]`.
2. Open the newest journal in `C:\Users\<user>\AppData\Local\Autodesk\Revit\Autodesk Revit 2023\Journals` and search for `Bibimcad`, `6f4c2a81`, `duplicated`, and `not signed`.
   - `duplicated` or `not signed as internal addin`: a second manifest exists, usually under `Program Files`. Disable it as described in **Verify**.
   - No Bibimcad lines at all: Revit did not find the manifest. Check its folder and extension (`.addin`, not `.addin.txt`).
3. Read `C:\Bibimcad\logs\Bibimcad.log`. A missing log alone does not identify the cause.
4. A DLL "blocked by Windows" needs `Unblock-File` on each of the four DLLs.

The tab appearing but a button failing is a different problem, usually missing or incomplete data. Read the button's message, then ask the user to click **08 Help**: it lists the problems it finds in `project_state.json` with step-by-step fixes.

## If walls land away from the CAD drawing

Ask the user to click **02 Coordinates** and read the "Link size check" lines.

- `ScaleMismatch` with ratio about 25.4: K1.dwg was linked as inches (Import units: Auto-Detect). Guide the user through the relink steps shown in **08 Help**. Do not change `mmPerCadUnit`, and do not move walls by hand.
- `Offset`: the DXF was saved from a moved or different copy of the drawing. Ask for a new DXF from the linked K1.dwg.
- `Inconclusive` or `Skipped`: the DXF path or `wallLayers` is wrong, or the link and DXF are different drawings. Read the message.

## Wall workflow setup

Installation does not need this. Do it only when the user asks to run buttons 03 to 07.

- `cadSource.dxfPath` must point at a DXF saved from `K1.dwg` as AutoCAD 2000 or later (default `C:\Bibimcad\input\K1.dxf`). DWG and R12 cannot be read. Ask the user for the file; do not convert it unless asked.
- `cadSource.unitCalibrationApproved` and `mmPerCadUnit` are human decisions. Do not change them.
- `cadSource.wallLayers` lists the wall layer names. Take them from the **03 Read DXF** report or from the user. Do not guess.
- `project.defaultWallHeightMm` (number) and `project.targetLevelName` (an existing Revit level name) are optional.
- Do not edit `wallCandidates`, `wallClassification`, or `validation` by hand. The commands own them.

The user order in Revit is 03 Read DXF, set `wallLayers`, 04 Classify, 05 Review, 06 Generate, 07 Validate.

Core tests run without Revit:

```powershell
dotnet test tests\Bibimcad.Core.Tests\Bibimcad.Core.Tests.csproj
```
