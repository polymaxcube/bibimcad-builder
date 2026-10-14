using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bibimcad.Services
{
    // Compares a built wall with its candidate. All values are millimeters in Revit model XY;
    // the wall sits on its level, so Z is not compared.
    public static class WallGeometryCheck
    {
        public const double DefaultToleranceMm = 1.0;

        public static string WallTypeName(double thicknessMm)
        {
            return "Bibimcad - " + RoundedThicknessMm(thicknessMm).ToString("0", CultureInfo.InvariantCulture) + " mm";
        }

        // Wall types are made in whole millimeters so one type serves every wall of that thickness.
        public static double RoundedThicknessMm(double thicknessMm)
        {
            return Math.Round(thicknessMm, 0, MidpointRounding.AwayFromZero);
        }

        public static List<string> Compare(ExpectedWall expected, BuiltWall built, double toleranceMm)
        {
            if (expected == null)
                throw new ArgumentNullException("expected");
            if (built == null)
                throw new ArgumentNullException("built");

            List<string> problems = new List<string>();

            double forward = Math.Max(
                Distance(expected.X1, expected.Y1, built.X1, built.Y1),
                Distance(expected.X2, expected.Y2, built.X2, built.Y2));
            double reverse = Math.Max(
                Distance(expected.X1, expected.Y1, built.X2, built.Y2),
                Distance(expected.X2, expected.Y2, built.X1, built.Y1));
            double endpointError = Math.Min(forward, reverse);
            if (endpointError > toleranceMm)
                problems.Add("Endpoint is " + Format(endpointError) + " mm from the mapped candidate point.");

            AddIfOff(problems, "Length", expected.LengthMm, built.LengthMm, toleranceMm);
            AddIfOff(problems, "Width", expected.ThicknessMm, built.WidthMm, toleranceMm);
            if (expected.HeightMm.HasValue)
                AddIfOff(problems, "Height", expected.HeightMm.Value, built.HeightMm, toleranceMm);

            return problems;
        }

        private static void AddIfOff(List<string> problems, string name, double expected, double actual, double toleranceMm)
        {
            if (Math.Abs(expected - actual) > toleranceMm)
                problems.Add(name + " is " + Format(actual) + " mm; expected " + Format(expected) + " mm.");
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public class ExpectedWall
    {
        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }

        public double LengthMm { get; set; }

        public double ThicknessMm { get; set; }

        public double? HeightMm { get; set; }
    }

    public class BuiltWall
    {
        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }

        public double LengthMm { get; set; }

        public double WidthMm { get; set; }

        public double HeightMm { get; set; }
    }
}
