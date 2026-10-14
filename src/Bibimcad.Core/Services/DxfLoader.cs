using System;
using System.IO;
using netDxf;
using netDxf.Header;

namespace Bibimcad.Services
{
    public static class DxfLoader
    {
        public static DxfDocument Load(string dxfPath)
        {
            if (string.IsNullOrWhiteSpace(dxfPath))
                throw new ArgumentException("DXF path is required.", "dxfPath");

            if (!File.Exists(dxfPath))
                throw new FileNotFoundException("DXF file was not found: " + dxfPath, dxfPath);

            if (string.Equals(Path.GetExtension(dxfPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Bibimcad reads DXF, not DWG. Save the drawing as AutoCAD 2000 or later DXF: " + dxfPath);
            }

            DxfVersion version = DxfDocument.CheckDxfFileVersion(dxfPath);
            if (version == DxfVersion.Unknown)
                throw new InvalidDataException("The file is not a readable DXF: " + dxfPath);

            if (version < DxfVersion.AutoCad2000)
            {
                throw new InvalidDataException(
                    "DXF version "
                    + version
                    + " is older than AutoCAD 2000. Save the drawing as AutoCAD 2000 or later DXF: "
                    + dxfPath);
            }

            DxfDocument document = DxfDocument.Load(dxfPath);
            if (document == null)
                throw new InvalidDataException("netDxf could not read the DXF file: " + dxfPath);

            return document;
        }
    }
}
