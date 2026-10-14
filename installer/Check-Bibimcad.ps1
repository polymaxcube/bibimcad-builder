# Checks a Bibimcad install for Revit 2023 and writes the result to the Desktop.
# Run with Revit closed:
#   powershell -ExecutionPolicy Bypass -File .\installer\Check-Bibimcad.ps1

$ErrorActionPreference = 'Continue'

$RevitYear = '2023'
$AddInId = '6f4c2a81-9e37-4b5d-8c14-2a7e9b0d4f63'
$FullClassName = 'Bibimcad.App'
$RevitInstallDir = "C:\Program Files\Autodesk\Revit $RevitYear"
$UserAddins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
$MachineAddins = Join-Path $env:ProgramData "Autodesk\Revit\Addins\$RevitYear"
$StatePath = 'C:\Bibimcad\data\project_state.json'
$LogPath = 'C:\Bibimcad\logs\Bibimcad.log'
$JournalDir = Join-Path $env:LOCALAPPDATA "Autodesk\Revit\Autodesk Revit $RevitYear\Journals"
$ReportPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Bibimcad_check.txt'

$lines = New-Object System.Collections.Generic.List[string]
$failures = 0

function Add-Line([string]$text) {
    $script:lines.Add($text)
    Write-Host $text
}

function Pass([string]$text) { Add-Line "[PASS] $text" }

function Fail([string]$text, [string]$fix) {
    $script:failures++
    Add-Line "[FAIL] $text"
    if ($fix) { Add-Line "       Fix: $fix" }
}

function Info([string]$text) { Add-Line "[INFO] $text" }

function Section([string]$title) {
    Add-Line ''
    Add-Line "== $title =="
}

Add-Line "Bibimcad install check - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Line "User: $env:USERNAME   Computer: $env:COMPUTERNAME"

Section 'Revit'
if (Test-Path (Join-Path $RevitInstallDir 'RevitAPI.dll')) {
    Pass "Revit $RevitYear API found in $RevitInstallDir"
} else {
    Fail "RevitAPI.dll not found in $RevitInstallDir" "Find the folder that contains RevitAPI.dll and build with -p:RevitInstallDir=`"<that folder>`""
}

$running = Get-Process -Name Revit -ErrorAction SilentlyContinue
if ($running) {
    Info "Revit is running. Close it, then start Revit $RevitYear again. Revit reads add-ins only at startup."
}

Section 'Manifest folders'
$manifests = @()
foreach ($dir in @($UserAddins, $MachineAddins)) {
    if (-not (Test-Path $dir)) {
        Info "Folder does not exist: $dir"
        continue
    }

    $names = Get-ChildItem $dir -File | Select-Object -ExpandProperty Name
    Info "Files in ${dir}: $(if ($names) { $names -join ', ' } else { '(empty)' })"

    foreach ($file in Get-ChildItem $dir -File) {
        if ($file.Name -like '*.addin.txt' -or $file.Name -like '*.addin.xml') {
            Fail "Wrong file extension: $($file.FullName)" "Rename it so the name ends in .addin"
        }
        if ($file.Extension -eq '.addin') {
            $manifests += $file
        }
    }
}

$bibimcadManifests = @()
foreach ($file in $manifests) {
    try {
        [xml]$xml = Get-Content $file.FullName -Raw
    } catch {
        if ((Get-Content $file.FullName -Raw) -match [regex]::Escape($AddInId)) {
            Fail "Bibimcad manifest is not valid XML: $($file.FullName)" "Copy installer\Bibimcad.addin again and edit only the <Assembly> line"
        }
        continue
    }

    foreach ($addIn in $xml.RevitAddIns.AddIn) {
        if ($addIn.AddInId -eq $AddInId) {
            $bibimcadManifests += [pscustomobject]@{ File = $file.FullName; AddIn = $addIn }
        }
    }
}

if ($bibimcadManifests.Count -eq 0) {
    Fail "No Bibimcad manifest found in Addins\$RevitYear" "Copy installer\Bibimcad.addin to $UserAddins"
} elseif ($bibimcadManifests.Count -gt 1) {
    Fail "Bibimcad manifest found $($bibimcadManifests.Count) times: $(($bibimcadManifests | ForEach-Object { $_.File }) -join ', ')" "Keep only $UserAddins\Bibimcad.addin. Revit rejects a duplicate AddInId."
} else {
    Pass "Manifest found: $($bibimcadManifests[0].File)"
}

Section 'Revit internal AddIns folder'
$internalAddins = Join-Path $RevitInstallDir 'AddIns'
if (Test-Path $internalAddins) {
    $internalCopies = Get-ChildItem $internalAddins -Recurse -File -Filter '*.addin' -ErrorAction SilentlyContinue |
        Where-Object { Select-String -Path $_.FullName -Pattern $AddInId -SimpleMatch -Quiet }
    if ($internalCopies) {
        foreach ($file in $internalCopies) {
            Fail "Bibimcad manifest inside Revit's own AddIns folder: $($file.FullName)" "Revit reports it as duplicated and not signed, and loads neither copy. From an administrator PowerShell run: Rename-Item `"$($file.FullName)`" `"$($file.Name).disabled`""
        }
    } else {
        Pass "No Bibimcad manifest under $internalAddins"
    }
} else {
    Info "Folder does not exist: $internalAddins"
}

Section 'Manifest contents'
$dllPath = $null
if ($bibimcadManifests.Count -ge 1) {
    $addIn = $bibimcadManifests[0].AddIn

    if ($addIn.Type -eq 'Application') { Pass 'Type is Application' } else { Fail "Type is '$($addIn.Type)'" 'Set <AddIn Type="Application">' }
    if ($addIn.FullClassName -eq $FullClassName) { Pass "FullClassName is $FullClassName" } else { Fail "FullClassName is '$($addIn.FullClassName)'" "Set it to $FullClassName" }

    $dllPath = "$($addIn.Assembly)".Trim()
    Info "Assembly: $dllPath"
    if ($dllPath -eq 'FULL_DLL_PATH_PLACEHOLDER') {
        Fail 'Assembly is still FULL_DLL_PATH_PLACEHOLDER' 'Replace it with the full path of Bibimcad.Addin.dll'
        $dllPath = $null
    } elseif (-not [System.IO.Path]::IsPathRooted($dllPath)) {
        Fail 'Assembly is not a full path' 'Use the full path, for example C:\bibimcad-builder-main\src\Bibimcad.Addin\bin\Debug\Bibimcad.Addin.dll'
    }
}

Section 'Add-in DLL'
if ($dllPath) {
    if (Test-Path $dllPath) {
        $dll = Get-Item $dllPath
        Pass "DLL exists ($($dll.Length) bytes, built $($dll.LastWriteTime))"

        $dllDir = Split-Path $dllPath
        $dependencies = @('Bibimcad.Core.dll', 'Newtonsoft.Json.dll', 'netDxf.dll')
        foreach ($name in $dependencies) {
            if (Test-Path (Join-Path $dllDir $name)) {
                Pass "$name is next to the DLL"
            } else {
                Fail "$name is missing next to the DLL" 'Run dotnet build again and keep the bin\Debug folder together'
            }
        }

        foreach ($name in @('Bibimcad.Addin.dll') + $dependencies) {
            $path = Join-Path $dllDir $name
            if ((Test-Path $path) -and (Get-Item $path -Stream Zone.Identifier -ErrorAction SilentlyContinue)) {
                Fail "$name is blocked by Windows (downloaded file)" "Run: Unblock-File `"$path`""
            }
        }

        try {
            $bytes = [System.IO.File]::ReadAllBytes($dllPath)
            $text = [System.Text.Encoding]::ASCII.GetString($bytes)
            if ($text -match '\.NETFramework,Version=v4\.8') {
                Pass 'DLL targets .NET Framework 4.8 (correct for Revit 2023)'
            } elseif ($text -match '\.NETCoreApp,Version=v(\d+\.\d+)') {
                Fail "DLL targets .NET $($Matches[1]). Revit $RevitYear cannot load it." 'Build the project unchanged; it must target net48'
            }
        } catch {
            Info "Could not read the DLL target framework: $($_.Exception.Message)"
        }
    } else {
        Fail "DLL not found: $dllPath" 'Run dotnet build in the project folder, or fix the path in the manifest'
    }
}

Section 'Project data'
if (Test-Path $StatePath) {
    Pass "$StatePath exists"
} else {
    Fail "$StatePath is missing (the tab still appears, the buttons fail)" 'Copy data\project_state.json from the project folder to C:\Bibimcad\data'
}

Section 'Bibimcad log'
if (Test-Path $LogPath) {
    Info "Last lines of ${LogPath}:"
    Get-Content $LogPath -Tail 20 | ForEach-Object { Add-Line "    $_" }
} else {
    Info "No log at $LogPath. Revit has not started Bibimcad yet."
}

Section 'Latest Revit journal'
if (Test-Path $JournalDir) {
    $journal = Get-ChildItem $JournalDir -Filter 'journal*.txt' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($journal) {
        Info "Journal: $($journal.FullName) ($($journal.LastWriteTime))"
        $hits = Select-String -Path $journal.FullName -Pattern 'Bibimcad|6f4c2a81|External Application|AddIn.*(fail|error)|duplicated|not signed' -ErrorAction SilentlyContinue | Select-Object -First 30
        if ($hits) {
            $hits | ForEach-Object { Add-Line "    $($_.LineNumber): $($_.Line.Trim())" }
        } else {
            Info 'No Bibimcad lines in this journal. Revit did not read the Bibimcad manifest in that session.'
        }
    } else {
        Info "No journal files in $JournalDir"
    }
} else {
    Info "Journal folder not found: $JournalDir"
}

Section 'Result'
if ($failures -eq 0) {
    Add-Line 'No problems found. Close Revit, start Revit 2023, and choose Always Load.'
} else {
    Add-Line "$failures problem(s) found. Apply the fixes above, then start Revit 2023 again."
}

$lines | Set-Content -Path $ReportPath -Encoding UTF8
Add-Line ''
Add-Line "Report saved to $ReportPath"
