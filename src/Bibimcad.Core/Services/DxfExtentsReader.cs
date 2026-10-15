using System;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;

namespace Bibimcad.Services
{
    // Extents of line work in model space, in millimeters, including lines inside block references.
    // Revit's CAD link shows block contents too, so both sides of the size check see the same geometry.
    public static class DxfExtentsReader
    {
        private const int MaxBlockDepth = 8;

        public static CadExtents Read(DxfDocument document, ICollection<string> layers, double mmPerCadUnit)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (mmPerCadUnit <= 0.0 || double.IsNaN(mmPerCadUnit) || double.IsInfinity(mmPerCadUnit))
                throw new ArgumentOutOfRangeException("mmPerCadUnit", "mmPerCadUnit must be a positive number.");

            HashSet<string> layerFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layers != null)
            {
                foreach (string layer in layers)
                {
                    if (!string.IsNullOrWhiteSpace(layer))
                        layerFilter.Add(layer.Trim());
                }
            }

            CadExtents extents = new CadExtents();
            foreach (EntityObject entity in document.Entities.All)
                Collect(entity, null, layerFilter, extents, 0);

            return extents.Scaled(mmPerCadUnit);
        }

        private static void Collect(
            EntityObject entity,
            string inheritedLayer,
            HashSet<string> layerFilter,
            CadExtents extents,
            int depth)
        {
            string layer = entity.Layer == null ? "0" : entity.Layer.Name;
            if (inheritedLayer != null && string.Equals(layer, "0", StringComparison.Ordinal))
                layer = inheritedLayer;

            Insert insert = entity as Insert;
            if (insert != null)
            {
                if (depth >= MaxBlockDepth)
                    return;

                List<EntityObject> parts;
                try
                {
                    parts = insert.Explode();
                }
                catch (Exception)
                {
                    return;
                }

                foreach (EntityObject part in parts)
                    Collect(part, layer, layerFilter, extents, depth + 1);
                return;
            }

            if (layerFilter.Count > 0 && !layerFilter.Contains(layer))
                return;

            Line line = entity as Line;
            if (line != null)
            {
                extents.Include(line.StartPoint.X, line.StartPoint.Y);
                extents.Include(line.EndPoint.X, line.EndPoint.Y);
                return;
            }

            Polyline2D polyline = entity as Polyline2D;
            if (polyline != null)
            {
                foreach (Polyline2DVertex vertex in polyline.Vertexes)
                {
                    Vector3 point = MathHelper.Transform(vertex.Position, polyline.Normal, polyline.Elevation);
                    extents.Include(point.X, point.Y);
                }

                return;
            }

            Polyline3D polyline3D = entity as Polyline3D;
            if (polyline3D != null)
            {
                foreach (Vector3 vertex in polyline3D.Vertexes)
                    extents.Include(vertex.X, vertex.Y);
            }
        }
    }
}
