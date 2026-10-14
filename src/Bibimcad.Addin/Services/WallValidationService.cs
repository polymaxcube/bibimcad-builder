using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Bibimcad.Models;
using Bibimcad.Utilities;

namespace Bibimcad.Services
{
    // Read-only check that each approved candidate has a Revit wall where the live CAD link puts it.
    public class WallValidationService
    {
        private readonly CoordinateTransformService _coordinates = new CoordinateTransformService();

        public ValidationInfo Validate(
            Document document,
            IEnumerable<WallCandidate> candidates,
            Transform linkTransform,
            double toleranceMm,
            DateTime validatedAt)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (candidates == null)
                throw new ArgumentNullException("candidates");
            if (linkTransform == null)
                throw new ArgumentNullException("linkTransform");

            ValidationInfo info = new ValidationInfo();
            info.ValidatedAt = validatedAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            info.ToleranceMm = toleranceMm;

            foreach (WallCandidate candidate in candidates)
            {
                if (!WallCandidateStatus.IsApproved(candidate))
                    continue;

                info.CheckedCount++;
                string reason = Check(document, candidate, linkTransform, toleranceMm);
                if (reason == null)
                {
                    info.PassedCount++;
                    continue;
                }

                info.FailedCount++;
                info.Failures.Add(new ValidationFailure
                {
                    CandidateId = candidate.Id,
                    RevitElementId = candidate.RevitElementId,
                    Reason = reason
                });
            }

            return info;
        }

        private string Check(Document document, WallCandidate candidate, Transform linkTransform, double toleranceMm)
        {
            if (!candidate.RevitElementId.HasValue)
                return "No Revit wall. Run 06 Generate.";

            Wall wall = document.GetElement(WallGenerationService.ToElementId(candidate.RevitElementId.Value)) as Wall;
            if (wall == null || !wall.IsValidObject)
            {
                return "Revit element "
                    + candidate.RevitElementId.Value.ToString(CultureInfo.InvariantCulture)
                    + " is missing or is not a wall. Run 06 Generate to recreate it.";
            }

            LocationCurve location = wall.Location as LocationCurve;
            if (location == null || location.Curve == null || !location.Curve.IsBound)
                return "The wall has no straight location line.";

            XYZ expectedStart = _coordinates.CadMillimetersToRevitPoint(candidate.StartXMm, candidate.StartYMm, 0.0, linkTransform);
            XYZ expectedEnd = _coordinates.CadMillimetersToRevitPoint(candidate.EndXMm, candidate.EndYMm, 0.0, linkTransform);
            XYZ builtStart = location.Curve.GetEndPoint(0);
            XYZ builtEnd = location.Curve.GetEndPoint(1);

            Parameter heightParameter = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            double heightFeet = heightParameter == null ? 0.0 : heightParameter.AsDouble();

            ExpectedWall expected = new ExpectedWall
            {
                X1 = RevitUnitUtils.FeetToMillimeters(expectedStart.X),
                Y1 = RevitUnitUtils.FeetToMillimeters(expectedStart.Y),
                X2 = RevitUnitUtils.FeetToMillimeters(expectedEnd.X),
                Y2 = RevitUnitUtils.FeetToMillimeters(expectedEnd.Y),
                LengthMm = candidate.LengthMm,
                ThicknessMm = candidate.ThicknessMm,
                HeightMm = candidate.HeightMm
            };
            BuiltWall built = new BuiltWall
            {
                X1 = RevitUnitUtils.FeetToMillimeters(builtStart.X),
                Y1 = RevitUnitUtils.FeetToMillimeters(builtStart.Y),
                X2 = RevitUnitUtils.FeetToMillimeters(builtEnd.X),
                Y2 = RevitUnitUtils.FeetToMillimeters(builtEnd.Y),
                LengthMm = RevitUnitUtils.FeetToMillimeters(location.Curve.Length),
                WidthMm = RevitUnitUtils.FeetToMillimeters(wall.Width),
                HeightMm = RevitUnitUtils.FeetToMillimeters(heightFeet)
            };

            List<string> problems = WallGeometryCheck.Compare(expected, built, toleranceMm);
            return problems.Count == 0 ? null : string.Join(" ", problems.ToArray());
        }
    }
}
