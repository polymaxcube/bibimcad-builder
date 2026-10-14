using System;
using System.Collections.Generic;
using System.Globalization;
using Bibimcad.Models;
using netDxf;
using netDxf.Entities;

namespace Bibimcad.Services
{
    public class CadSegmentExtractor
    {
        public SegmentExtraction Extract(DxfDocument document, IEnumerable<string> wallLayers, double mmPerCadUnit)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (wallLayers == null)
                throw new ArgumentNullException("wallLayers");
            if (mmPerCadUnit <= 0.0 || double.IsNaN(mmPerCadUnit) || double.IsInfinity(mmPerCadUnit))
                throw new ArgumentOutOfRangeException("mmPerCadUnit", "mmPerCadUnit must be a positive number.");

            HashSet<string> layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string layer in wallLayers)
            {
                if (!string.IsNullOrWhiteSpace(layer))
                    layers.Add(layer.Trim());
            }

            SegmentExtraction result = new SegmentExtraction();
            Dictionary<string, int> perLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string layer in layers)
                perLayer[layer] = 0;

            foreach (EntityObject entity in document.Entities.All)
            {
                string layerName = entity.Layer == null ? "0" : entity.Layer.Name;
                if (!layers.Contains(layerName))
                    continue;

                int before = result.Segments.Count;
                Line line = entity as Line;
                Polyline2D polyline = entity as Polyline2D;
                if (line != null)
                {
                    AddSegment(
                        result,
                        line.Handle,
                        layerName,
                        line.StartPoint.X,
                        line.StartPoint.Y,
                        line.EndPoint.X,
                        line.EndPoint.Y,
                        mmPerCadUnit);
                }
                else if (polyline != null)
                {
                    AddPolylineSegments(result, polyline, layerName, mmPerCadUnit);
                }
                else
                {
                    result.IgnoredEntityCount++;
                }

                perLayer[layerName] = perLayer[layerName] + result.Segments.Count - before;
            }

            foreach (KeyValuePair<string, int> pair in perLayer)
            {
                if (pair.Value == 0)
                    result.Warnings.Add("Wall layer " + pair.Key + " has no LINE or LWPOLYLINE segments in model space.");
            }

            if (result.ArcSegmentCount > 0)
            {
                result.Warnings.Add(
                    result.ArcSegmentCount.ToString(CultureInfo.InvariantCulture)
                    + " curved polyline segments on wall layers were skipped. Curved walls are not classified.");
            }

            if (result.IgnoredEntityCount > 0)
            {
                result.Warnings.Add(
                    result.IgnoredEntityCount.ToString(CultureInfo.InvariantCulture)
                    + " entities on wall layers are not LINE or LWPOLYLINE and were skipped.");
            }

            return result;
        }

        private static void AddPolylineSegments(
            SegmentExtraction result,
            Polyline2D polyline,
            string layerName,
            double mmPerCadUnit)
        {
            List<Polyline2DVertex> vertices = polyline.Vertexes;
            int count = vertices.Count;
            int segmentCount = polyline.IsClosed ? count : count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Polyline2DVertex start = vertices[i];
                Polyline2DVertex end = vertices[(i + 1) % count];
                if (Math.Abs(start.Bulge) > 1e-12)
                {
                    result.ArcSegmentCount++;
                    continue;
                }

                Vector3 a = MathHelper.Transform(start.Position, polyline.Normal, polyline.Elevation);
                Vector3 b = MathHelper.Transform(end.Position, polyline.Normal, polyline.Elevation);
                AddSegment(
                    result,
                    polyline.Handle + ":" + i.ToString(CultureInfo.InvariantCulture),
                    layerName,
                    a.X,
                    a.Y,
                    b.X,
                    b.Y,
                    mmPerCadUnit);
            }
        }

        private static void AddSegment(
            SegmentExtraction result,
            string id,
            string layer,
            double x1,
            double y1,
            double x2,
            double y2,
            double mmPerCadUnit)
        {
            if (Math.Abs(x2 - x1) < 1e-9 && Math.Abs(y2 - y1) < 1e-9)
            {
                result.ZeroLengthSegmentCount++;
                return;
            }

            result.Segments.Add(new CadSegment(
                id,
                layer,
                x1 * mmPerCadUnit,
                y1 * mmPerCadUnit,
                x2 * mmPerCadUnit,
                y2 * mmPerCadUnit));
        }
    }

    public class SegmentExtraction
    {
        public SegmentExtraction()
        {
            Segments = new List<CadSegment>();
            Warnings = new List<string>();
        }

        public List<CadSegment> Segments { get; private set; }

        public int ArcSegmentCount { get; set; }

        public int ZeroLengthSegmentCount { get; set; }

        public int IgnoredEntityCount { get; set; }

        public List<string> Warnings { get; private set; }
    }
}
