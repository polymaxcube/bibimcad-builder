using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Bibimcad.Utilities;

namespace Bibimcad.Services
{
    // Extents of the CAD link's line work where Revit actually draws it, expressed in the same
    // CAD-local millimeters that Generate feeds through the link transform. If Revit scaled the
    // import (for example as inches), the scale shows up here and not in GetTotalTransform().
    public static class LinkGeometryReader
    {
        private const int MaxDepth = 16;

        public static CadExtents ReadLocalExtentsMm(
            Document document,
            ImportInstance importInstance,
            Transform linkTransform,
            ICollection<string> layers)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (importInstance == null)
                throw new ArgumentNullException("importInstance");
            if (linkTransform == null)
                throw new ArgumentNullException("linkTransform");

            HashSet<string> layerFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layers != null)
            {
                foreach (string layer in layers)
                {
                    if (!string.IsNullOrWhiteSpace(layer))
                        layerFilter.Add(layer.Trim());
                }
            }

            Reader reader = new Reader(document, linkTransform.Inverse, layerFilter);
            Options options = new Options();
            options.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geometry = importInstance.get_Geometry(options);
            if (geometry != null)
                reader.Collect(geometry, Transform.Identity, 0);

            return reader.Extents;
        }

        private sealed class Reader
        {
            private readonly Document _document;
            private readonly Transform _modelToLocal;
            private readonly HashSet<string> _layerFilter;
            private readonly Dictionary<int, string> _layerNames = new Dictionary<int, string>();

            public Reader(Document document, Transform modelToLocal, HashSet<string> layerFilter)
            {
                _document = document;
                _modelToLocal = modelToLocal;
                _layerFilter = layerFilter;
                Extents = new CadExtents();
            }

            public CadExtents Extents { get; private set; }

            public void Collect(GeometryElement geometry, Transform toModel, int depth)
            {
                if (depth > MaxDepth)
                    return;

                foreach (GeometryObject geometryObject in geometry)
                {
                    GeometryInstance instance = geometryObject as GeometryInstance;
                    if (instance != null)
                    {
                        GeometryElement symbol = instance.GetSymbolGeometry();
                        if (symbol != null)
                            Collect(symbol, toModel.Multiply(instance.Transform), depth + 1);
                        continue;
                    }

                    if (!IsOnCheckedLayer(geometryObject))
                        continue;

                    Line line = geometryObject as Line;
                    if (line != null && line.IsBound)
                    {
                        Include(toModel.OfPoint(line.GetEndPoint(0)));
                        Include(toModel.OfPoint(line.GetEndPoint(1)));
                        continue;
                    }

                    PolyLine polyLine = geometryObject as PolyLine;
                    if (polyLine != null)
                    {
                        foreach (XYZ point in polyLine.GetCoordinates())
                            Include(toModel.OfPoint(point));
                    }
                }
            }

            private void Include(XYZ modelPoint)
            {
                XYZ local = _modelToLocal.OfPoint(modelPoint);
                Extents.Include(
                    RevitUnitUtils.FeetToMillimeters(local.X),
                    RevitUnitUtils.FeetToMillimeters(local.Y));
            }

            private bool IsOnCheckedLayer(GeometryObject geometryObject)
            {
                if (_layerFilter.Count == 0)
                    return true;

                string layer = GetLayerName(geometryObject.GraphicsStyleId);
                return layer != null && _layerFilter.Contains(layer);
            }

            private string GetLayerName(ElementId styleId)
            {
                if (styleId == null || styleId == ElementId.InvalidElementId)
                    return null;

                string name;
                if (_layerNames.TryGetValue(styleId.IntegerValue, out name))
                    return name;

                GraphicsStyle style = _document.GetElement(styleId) as GraphicsStyle;
                name = style == null || style.GraphicsStyleCategory == null
                    ? null
                    : style.GraphicsStyleCategory.Name;
                _layerNames[styleId.IntegerValue] = name;
                return name;
            }
        }
    }
}
