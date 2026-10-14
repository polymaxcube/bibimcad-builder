# Rebuilds sample_walls.dxf, the drawing the Core tests read.
# Run after building the solution:
#   powershell -ExecutionPolicy Bypass -File .\tests\data\New-SampleWallsDxf.ps1
#
# Units are millimeters (INSUNITS = Millimeters). Layer A-WALL holds 5 LINEs and 2 LWPOLYLINEs:
#   Wall 1  horizontal, two LINEs at y=0 and y=200, x 0..6000      -> 200 mm wall, centerline y=100
#   Wall 2  closed LWPOLYLINE 150 x 3000 at x 6000..6150, y 1000..4000 -> 150 mm wall, 2 end caps unpaired
#   Wall 3  45 degree LINEs 200 mm apart, 2828.43 mm long           -> 200 mm angled wall
#   Noise   one LINE at y=0, x 10000..12000                         -> unpaired
#   Arc     LWPOLYLINE with one bulged segment and one straight one -> 1 straight segment, unpaired
# Other layers:
#   A-ANNO  LINE at y=-100 parallel to wall 1 (must be ignored by the wall layer filter)
#   A-DOOR  one INSERT of block DOOR

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dll = Get-ChildItem -Path (Join-Path $root 'src\Bibimcad.Core\bin') -Recurse -Filter netDxf.dll | Select-Object -First 1
if (-not $dll) {
    throw 'netDxf.dll not found. Run dotnet build first.'
}
Add-Type -Path $dll.FullName

function V([double]$x, [double]$y) { New-Object netDxf.Vector2 -ArgumentList $x, $y }

function Add-Line($document, $layer, [double]$x1, [double]$y1, [double]$x2, [double]$y2) {
    $line = New-Object netDxf.Entities.Line -ArgumentList (V $x1 $y1), (V $x2 $y2)
    $line.Layer = $layer
    $document.Entities.Add($line)
}

$document = New-Object netDxf.DxfDocument -ArgumentList ([netDxf.Header.DxfVersion]::AutoCad2000)
$document.DrawingVariables.InsUnits = [netDxf.Units.DrawingUnits]::Millimeters

$wall = New-Object netDxf.Tables.Layer -ArgumentList 'A-WALL'
$anno = New-Object netDxf.Tables.Layer -ArgumentList 'A-ANNO'
$door = New-Object netDxf.Tables.Layer -ArgumentList 'A-DOOR'

Add-Line $document $wall 0 0 6000 0
Add-Line $document $wall 0 200 6000 200

$rectangle = New-Object 'System.Collections.Generic.List[netDxf.Entities.Polyline2DVertex]'
$rectangle.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 6000, 1000))
$rectangle.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 6150, 1000))
$rectangle.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 6150, 4000))
$rectangle.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 6000, 4000))
$polyline = New-Object netDxf.Entities.Polyline2D -ArgumentList $rectangle, $true
$polyline.Layer = $wall
$document.Entities.Add($polyline)

$offset = 200 / [Math]::Sqrt(2)
Add-Line $document $wall 0 6000 2000 8000
Add-Line $document $wall (-$offset) (6000 + $offset) (2000 - $offset) (8000 + $offset)

Add-Line $document $wall 10000 0 12000 0

$arcVertices = New-Object 'System.Collections.Generic.List[netDxf.Entities.Polyline2DVertex]'
$arcVertices.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 20000, 0, 0.5))
$arcVertices.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 22000, 0))
$arcVertices.Add((New-Object netDxf.Entities.Polyline2DVertex -ArgumentList 24000, 0))
$arcPolyline = New-Object netDxf.Entities.Polyline2D -ArgumentList $arcVertices, $false
$arcPolyline.Layer = $wall
$document.Entities.Add($arcPolyline)

Add-Line $document $anno 0 -100 6000 -100

$leaf = New-Object netDxf.Entities.Line -ArgumentList (V 0 0), (V 900 0)
$blockEntities = New-Object 'System.Collections.Generic.List[netDxf.Entities.EntityObject]'
$blockEntities.Add($leaf)
$block = New-Object netDxf.Blocks.Block -ArgumentList 'DOOR', $blockEntities
$insert = New-Object netDxf.Entities.Insert -ArgumentList $block, (V 3000 0)
$insert.Layer = $door
$document.Entities.Add($insert)

$output = Join-Path $PSScriptRoot 'sample_walls.dxf'
if (-not $document.Save($output)) {
    throw "netDxf could not save $output"
}
"Saved $output"
