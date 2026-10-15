using System;
using System.Collections.Generic;
using System.Globalization;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    // Decides whether the CAD link in Revit has the same size and position as the DXF.
    public static class LinkSizeCheck
    {
        public const double RatioTolerance = 0.01;
        public const double OffsetToleranceMm = 5.0;
        private const double MinMeasurableMm = 1.0;

        // Revit size divided by DXF size when the link was imported in that unit instead of millimeters.
        private static readonly KeyValuePair<string, double>[] KnownUnits = new[]
        {
            new KeyValuePair<string, double>("Inch", 25.4),
            new KeyValuePair<string, double>("Foot", 304.8),
            new KeyValuePair<string, double>("Centimeter", 10.0),
            new KeyValuePair<string, double>("Decimeter", 100.0),
            new KeyValuePair<string, double>("Meter", 1000.0),
        };

        public static LinkSizeCheckInfo Evaluate(
            CadExtents dxfMm,
            CadExtents linkMm,
            IEnumerable<string> layers,
            DateTime checkedAt)
        {
            if (dxfMm == null)
                throw new ArgumentNullException("dxfMm");
            if (linkMm == null)
                throw new ArgumentNullException("linkMm");

            LinkSizeCheckInfo info = new LinkSizeCheckInfo();
            info.CheckedAt = checkedAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            if (layers != null)
                info.Layers.AddRange(layers);

            if (dxfMm.IsEmpty)
                return Inconclusive(info, "No line work was found in the DXF on the checked layers.");
            if (linkMm.IsEmpty)
                return Inconclusive(info, "No line work was found in the Revit CAD link on the checked layers.");

            info.DxfWidthMm = Round(dxfMm.Width);
            info.DxfHeightMm = Round(dxfMm.Height);
            info.LinkWidthMm = Round(linkMm.Width);
            info.LinkHeightMm = Round(linkMm.Height);

            double? ratio = MeasureRatio(dxfMm, linkMm);
            if (!ratio.HasValue)
            {
                return Inconclusive(
                    info,
                    "The width and height ratios of the link and the DXF disagree. The link and the DXF may be different drawings, or the layers differ.");
            }

            info.Ratio = Math.Round(ratio.Value, 4);
            if (Math.Abs(ratio.Value - 1.0) <= RatioTolerance)
            {
                info.OffsetXMm = Round(linkMm.MinX - dxfMm.MinX);
                info.OffsetYMm = Round(linkMm.MinY - dxfMm.MinY);
                if (Math.Abs(info.OffsetXMm.Value) <= OffsetToleranceMm && Math.Abs(info.OffsetYMm.Value) <= OffsetToleranceMm)
                {
                    info.Result = LinkSizeCheckResult.Passed;
                    info.Message = "The link and the DXF have the same size and position.";
                    return info;
                }

                info.Result = LinkSizeCheckResult.Offset;
                info.Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "The link has the right size but its drawing is shifted by X {0:0.#} mm, Y {1:0.#} mm from the DXF. "
                        + "The DXF was probably saved from a moved or different copy of the drawing.",
                    info.OffsetXMm.Value,
                    info.OffsetYMm.Value);
                return info;
            }

            info.Result = LinkSizeCheckResult.ScaleMismatch;
            bool bigger = ratio.Value > 1.0;
            double factor = bigger ? ratio.Value : 1.0 / ratio.Value;
            string unit = MatchUnit(factor);
            string size = string.Format(CultureInfo.InvariantCulture, "{0:0.##}", factor);

            if (bigger && unit != null)
            {
                info.LikelyImportUnit = unit;
                info.Message = "Revit draws the link " + size + " times bigger than the DXF. "
                    + "It was linked with Import units = " + unit + " (often from Auto-Detect). "
                    + "Relink it with Import units = Millimeter.";
            }
            else if (bigger)
            {
                info.Message = "Revit draws the link " + size + " times bigger than the DXF. "
                    + "Relink it with Import units = Millimeter, and check that the link and the DXF are the same drawing.";
            }
            else
            {
                info.Message = "Revit draws the link " + size + " times smaller than the DXF. "
                    + "Either the link was imported in the wrong unit, or cadSource.mmPerCadUnit is wrong.";
            }

            return info;
        }

        public static LinkSizeCheckInfo Skipped(string reason, DateTime checkedAt)
        {
            LinkSizeCheckInfo info = new LinkSizeCheckInfo();
            info.CheckedAt = checkedAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            info.Result = LinkSizeCheckResult.Skipped;
            info.Message = reason;
            return info;
        }

        private static double? MeasureRatio(CadExtents dxfMm, CadExtents linkMm)
        {
            double? ratioX = dxfMm.Width >= MinMeasurableMm && linkMm.Width >= MinMeasurableMm
                ? linkMm.Width / dxfMm.Width
                : (double?)null;
            double? ratioY = dxfMm.Height >= MinMeasurableMm && linkMm.Height >= MinMeasurableMm
                ? linkMm.Height / dxfMm.Height
                : (double?)null;

            if (ratioX.HasValue && ratioY.HasValue)
            {
                if (Math.Abs(ratioX.Value - ratioY.Value) > RatioTolerance * Math.Max(ratioX.Value, ratioY.Value) * 2.0)
                    return null;
                return (ratioX.Value + ratioY.Value) / 2.0;
            }

            return ratioX ?? ratioY;
        }

        private static string MatchUnit(double factor)
        {
            foreach (KeyValuePair<string, double> unit in KnownUnits)
            {
                if (Math.Abs(factor / unit.Value - 1.0) <= RatioTolerance)
                    return unit.Key;
            }

            return null;
        }

        private static LinkSizeCheckInfo Inconclusive(LinkSizeCheckInfo info, string message)
        {
            info.Result = LinkSizeCheckResult.Inconclusive;
            info.Message = message;
            return info;
        }

        private static double Round(double value)
        {
            return Math.Round(value, 1);
        }
    }
}
