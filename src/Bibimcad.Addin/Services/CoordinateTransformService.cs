using System;
using System.Globalization;
using Autodesk.Revit.DB;
using Bibimcad.Models;
using Bibimcad.Utilities;

namespace Bibimcad.Services
{
    public class CoordinateTransformService
    {
        public const double KnownCalibrationDistanceMillimeters = 6000.0;
        public const double DistanceToleranceMillimeters = 1.0;
        private const double DegreesPerRadian = 180.0 / Math.PI;

        public CadTransformInfo Capture(Transform transform)
        {
            if (transform == null)
                throw new ArgumentNullException("transform");

            CadTransformInfo info = new CadTransformInfo();
            info.OriginXFeet = transform.Origin.X;
            info.OriginYFeet = transform.Origin.Y;
            info.OriginZFeet = transform.Origin.Z;
            info.BasisX_X = transform.BasisX.X;
            info.BasisX_Y = transform.BasisX.Y;
            info.BasisX_Z = transform.BasisX.Z;
            info.BasisY_X = transform.BasisY.X;
            info.BasisY_Y = transform.BasisY.Y;
            info.BasisY_Z = transform.BasisY.Z;
            info.BasisZ_X = transform.BasisZ.X;
            info.BasisZ_Y = transform.BasisZ.Y;
            info.BasisZ_Z = transform.BasisZ.Z;
            info.RotationDegrees = Math.Atan2(transform.BasisX.Y, transform.BasisX.X) * DegreesPerRadian;
            info.ScaleX = transform.BasisX.GetLength();
            info.ScaleY = transform.BasisY.GetLength();
            info.ScaleZ = transform.BasisZ.GetLength();
            info.IsMirrored = transform.Determinant < 0.0;
            return info;
        }

        public XYZ CadMillimetersToLocalRevitPoint(double xMm, double yMm, double zMm)
        {
            return new XYZ(
                RevitUnitUtils.MillimetersToFeet(xMm),
                RevitUnitUtils.MillimetersToFeet(yMm),
                RevitUnitUtils.MillimetersToFeet(zMm));
        }

        public XYZ CadMillimetersToRevitPoint(
            double xMm,
            double yMm,
            double zMm,
            Transform linkTransform)
        {
            if (linkTransform == null)
                throw new ArgumentNullException("linkTransform");

            XYZ localPoint = CadMillimetersToLocalRevitPoint(xMm, yMm, zMm);
            return linkTransform.OfPoint(localPoint);
        }

        public KnownDistanceCheck ValidateKnownDistance(Transform linkTransform)
        {
            if (linkTransform == null)
                throw new ArgumentNullException("linkTransform");

            XYZ start = CadMillimetersToRevitPoint(0.0, 0.0, 0.0, linkTransform);
            XYZ end = CadMillimetersToRevitPoint(
                KnownCalibrationDistanceMillimeters,
                0.0,
                0.0,
                linkTransform);

            double measuredMillimeters = RevitUnitUtils.FeetToMillimeters(start.DistanceTo(end));
            double delta = Math.Abs(measuredMillimeters - KnownCalibrationDistanceMillimeters);
            bool passed = delta <= DistanceToleranceMillimeters;

            return new KnownDistanceCheck(
                KnownCalibrationDistanceMillimeters,
                measuredMillimeters,
                DistanceToleranceMillimeters,
                passed);
        }
    }

    public sealed class KnownDistanceCheck
    {
        public KnownDistanceCheck(
            double expectedMillimeters,
            double measuredMillimeters,
            double toleranceMillimeters,
            bool passed)
        {
            ExpectedMillimeters = expectedMillimeters;
            MeasuredMillimeters = measuredMillimeters;
            ToleranceMillimeters = toleranceMillimeters;
            Passed = passed;
        }

        public double ExpectedMillimeters { get; private set; }

        public double MeasuredMillimeters { get; private set; }

        public double ToleranceMillimeters { get; private set; }

        public bool Passed { get; private set; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "expected {0} mm, measured {1} mm, tolerance {2} mm, passed {3}",
                ExpectedMillimeters,
                MeasuredMillimeters,
                ToleranceMillimeters,
                Passed);
        }
    }
}
