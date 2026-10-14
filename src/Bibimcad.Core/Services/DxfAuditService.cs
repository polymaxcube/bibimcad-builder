using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bibimcad.Models;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace Bibimcad.Services
{
    public class DxfAuditService
    {
        // Fills cadAudit from the model space of the DXF and copies INSUNITS into cadSource.headerInsUnits.
        // The approved unit and mmPerCadUnit are a human decision and are never changed here.
        public CadAuditInfo Audit(DxfDocument document, string sourcePath, CadSourceInfo cadSource, DateTime readAt)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (cadSource == null)
                throw new ArgumentNullException("cadSource");

            CadAuditInfo audit = new CadAuditInfo();
            audit.SourceFile = sourcePath;
            audit.DxfVersion = document.DrawingVariables.AcadVer.ToString();
            audit.ReadAt = readAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            Dictionary<string, int> perLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Layer layer in document.Layers)
                perLayer[layer.Name] = 0;

            Dictionary<string, int> perType = new Dictionary<string, int>(StringComparer.Ordinal);
            int insertCount = 0;
            int entityCount = 0;
            foreach (EntityObject entity in document.Entities.All)
            {
                entityCount++;
                string typeName = entity.CodeName;
                int count;
                perType.TryGetValue(typeName, out count);
                perType[typeName] = count + 1;

                string layerName = entity.Layer == null ? "0" : entity.Layer.Name;
                perLayer.TryGetValue(layerName, out count);
                perLayer[layerName] = count + 1;

                if (entity is Insert)
                    insertCount++;
            }

            audit.LayerCount = document.Layers.Count;
            audit.BlockCount = CountUserBlocks(document);
            foreach (KeyValuePair<string, int> pair in perType.OrderBy(p => p.Key, StringComparer.Ordinal))
                audit.EntityCounts[pair.Key] = pair.Value;

            foreach (KeyValuePair<string, int> pair in perLayer.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                audit.Layers.Add(new CadLayerInfo { Name = pair.Key, EntityCount = pair.Value });

            string headerUnits = document.DrawingVariables.InsUnits.ToString();
            cadSource.HeaderInsUnits = headerUnits;

            if (entityCount == 0)
                audit.Warnings.Add("Model space has no entities.");

            if (!string.IsNullOrWhiteSpace(cadSource.ApprovedCoordinateUnit)
                && !string.Equals(headerUnits, cadSource.ApprovedCoordinateUnit.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                audit.Warnings.Add(
                    "DXF header INSUNITS is "
                    + headerUnits
                    + ", but the approved coordinate unit is "
                    + cadSource.ApprovedCoordinateUnit
                    + ". Bibimcad uses the approved unit and mmPerCadUnit "
                    + cadSource.MmPerCadUnit.ToString("0.######", CultureInfo.InvariantCulture)
                    + ".");
            }

            if (!cadSource.UnitCalibrationApproved)
                audit.Warnings.Add("Unit calibration is not approved. Classification will not run until it is.");

            if (insertCount > 0)
            {
                audit.Warnings.Add(
                    insertCount.ToString(CultureInfo.InvariantCulture)
                    + " block references (INSERT) are in model space. Lines inside blocks are not read as wall edges.");
            }

            return audit;
        }

        private static int CountUserBlocks(DxfDocument document)
        {
            int count = 0;
            foreach (Block block in document.Blocks)
            {
                if (!block.Name.StartsWith("*", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }
    }
}
