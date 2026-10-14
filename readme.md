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

`Newtonsoft.Json.dll` is written next to it. Leave both files there.

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

On startup Revit loads `Bibimcad.App` from `App.cs`. That creates a **Bibimcad** tab with a **K1 Workflow** panel and two buttons:

- **01 Inspect** reads the project state.
- **02 Coordinates** reads the linked CAD transform and saves it.

There is no `dotnet run`. Revit is the program that runs the add-in.

## After you change the code

Run `dotnet build` again, then restart Revit. Revit keeps the DLL loaded until it closes.
