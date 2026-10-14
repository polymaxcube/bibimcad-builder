using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bibimcad.Models;
using Bibimcad.Services;
using Bibimcad.UI;
using Bibimcad.Utilities;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdCheckCoordinates : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                UIDocument uiDocument = commandData.Application == null
                    ? null
                    : commandData.Application.ActiveUIDocument;
                Document document = uiDocument == null ? null : uiDocument.Document;
                if (document == null)
                {
                    BibimcadDialog.Show("Open a project.", "Open a Revit project before checking coordinates.");
                    message = "No active Revit project.";
                    return Result.Failed;
                }

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                if (state == null || state.CadSource == null || string.IsNullOrWhiteSpace(state.CadSource.DwgName))
                {
                    BibimcadDialog.Show("Project state is incomplete.", "CAD source DWG name is missing from project_state.json.");
                    message = "CAD source DWG name is missing.";
                    return Result.Failed;
                }

                string dwgName = state.CadSource.DwgName.Trim();
                ImportInstance importInstance;
                if (!CadLinkPicker.TryResolve(uiDocument, document, dwgName, out importInstance))
                    return Result.Cancelled;

                Transform transform = importInstance.GetTotalTransform();
                if (transform == null)
                    throw new InvalidOperationException("ImportInstance.GetTotalTransform() returned no transform.");

                CoordinateTransformService coordinateService = new CoordinateTransformService();
                CadTransformInfo transformInfo = coordinateService.Capture(transform);
                string cadName = ElementNameUtils.GetCadName(document, importInstance);

                state.RevitLink = new RevitLinkInfo();
                state.RevitLink.ElementId = importInstance.Id.IntegerValue;
                state.RevitLink.Name = cadName;
                state.RevitLink.IsLinked = importInstance.IsLinked;
                state.RevitLink.Transform = transformInfo;
                repository.Save(state);

                KnownDistanceCheck distanceCheck = coordinateService.ValidateKnownDistance(transform);
                BibimcadDialog.Show(
                    distanceCheck.Passed ? "CAD coordinates" : "Distance check failed",
                    BuildReport(importInstance, cadName, transformInfo, distanceCheck));

                LoggingUtils.Info(
                    "Saved CAD transform for element "
                    + importInstance.Id.IntegerValue.ToString(CultureInfo.InvariantCulture)
                    + ". Distance check "
                    + (distanceCheck.Passed ? "passed." : "failed."));

                if (!distanceCheck.Passed)
                {
                    message = "The 6000 mm distance check failed.";
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return BibimcadDialog.Fail(ref message, "Coordinate check failed.", ex);
            }
        }

        private static string BuildReport(
            ImportInstance importInstance,
            string cadName,
            CadTransformInfo transformInfo,
            KnownDistanceCheck distanceCheck)
        {
            if (string.IsNullOrWhiteSpace(cadName))
                cadName = "(name unresolved)";

            double originXmm = RevitUnitUtils.FeetToMillimeters(transformInfo.OriginXFeet);
            double originYmm = RevitUnitUtils.FeetToMillimeters(transformInfo.OriginYFeet);
            double originZmm = RevitUnitUtils.FeetToMillimeters(transformInfo.OriginZFeet);

            List<string> lines = new List<string>();
            lines.Add("CAD name: " + cadName);
            lines.Add("Element ID: " + importInstance.Id.IntegerValue.ToString(CultureInfo.InvariantCulture));
            lines.Add("Is linked: " + (importInstance.IsLinked ? "Yes" : "No"));
            lines.Add("Origin X: " + FormatDecimal(originXmm) + " mm");
            lines.Add("Origin Y: " + FormatDecimal(originYmm) + " mm");
            lines.Add("Origin Z: " + FormatDecimal(originZmm) + " mm");
            lines.Add("Rotation: " + FormatDecimal(transformInfo.RotationDegrees) + " degrees");
            lines.Add("Scale X: " + FormatDecimal(transformInfo.ScaleX));
            lines.Add("Scale Y: " + FormatDecimal(transformInfo.ScaleY));
            lines.Add("Scale Z: " + FormatDecimal(transformInfo.ScaleZ));
            lines.Add("Mirrored: " + (transformInfo.IsMirrored ? "Yes" : "No"));
            lines.Add(
                FormatDecimal(distanceCheck.ExpectedMillimeters)
                + " mm distance check: "
                + (distanceCheck.Passed ? "Passed" : "Failed")
                + " ("
                + FormatDecimal(distanceCheck.MeasuredMillimeters)
                + " mm)");
            lines.Add("Saved: " + PathUtils.GetProjectStatePath());
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string FormatDecimal(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
