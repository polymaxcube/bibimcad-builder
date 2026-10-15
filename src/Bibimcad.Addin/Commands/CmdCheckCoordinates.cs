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

                LinkSizeCheckInfo sizeCheck = RunSizeCheck(document, importInstance, transform, state.CadSource, DateTime.Now);

                state.RevitLink = new RevitLinkInfo();
                state.RevitLink.ElementId = importInstance.Id.IntegerValue;
                state.RevitLink.Name = cadName;
                state.RevitLink.IsLinked = importInstance.IsLinked;
                state.RevitLink.Transform = transformInfo;
                state.RevitLink.SizeCheck = sizeCheck;
                repository.Save(state);

                KnownDistanceCheck distanceCheck = coordinateService.ValidateKnownDistance(transform);
                string title = !distanceCheck.Passed
                    ? "Distance check failed"
                    : LinkSizeCheckResult.IsProblem(sizeCheck) ? "Link size check failed" : "CAD coordinates";
                BibimcadDialog.Show(title, BuildReport(importInstance, cadName, transformInfo, distanceCheck, sizeCheck));

                LoggingUtils.Info(
                    "Saved CAD transform for element "
                    + importInstance.Id.IntegerValue.ToString(CultureInfo.InvariantCulture)
                    + ". Distance check "
                    + (distanceCheck.Passed ? "passed." : "failed.")
                    + " Link size check: "
                    + sizeCheck.Result
                    + (sizeCheck.Ratio.HasValue ? " (ratio " + sizeCheck.Ratio.Value.ToString("0.####", CultureInfo.InvariantCulture) + ")" : string.Empty)
                    + ".");

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

        private static LinkSizeCheckInfo RunSizeCheck(
            Document document,
            ImportInstance importInstance,
            Transform transform,
            CadSourceInfo cadSource,
            DateTime now)
        {
            if (string.IsNullOrWhiteSpace(cadSource.DxfPath))
                return LinkSizeCheck.Skipped("cadSource.dxfPath is empty, so the link size was not checked.", now);
            if (cadSource.MmPerCadUnit <= 0.0)
                return LinkSizeCheck.Skipped("cadSource.mmPerCadUnit must be positive to check the link size.", now);

            List<string> layers = new List<string>();
            if (cadSource.WallLayers != null)
            {
                foreach (string layer in cadSource.WallLayers)
                {
                    if (!string.IsNullOrWhiteSpace(layer))
                        layers.Add(layer.Trim());
                }
            }

            netDxf.DxfDocument dxf;
            try
            {
                dxf = DxfLoader.Load(cadSource.DxfPath);
            }
            catch (Exception ex)
            {
                return LinkSizeCheck.Skipped("The DXF could not be read, so the link size was not checked: " + ex.Message, now);
            }

            CadExtents dxfExtents = DxfExtentsReader.Read(dxf, layers, cadSource.MmPerCadUnit);
            CadExtents linkExtents = LinkGeometryReader.ReadLocalExtentsMm(document, importInstance, transform, layers);
            return LinkSizeCheck.Evaluate(dxfExtents, linkExtents, layers, now);
        }

        private static string BuildReport(
            ImportInstance importInstance,
            string cadName,
            CadTransformInfo transformInfo,
            KnownDistanceCheck distanceCheck,
            LinkSizeCheckInfo sizeCheck)
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

            lines.Add(string.Empty);
            lines.Add("Link size check ("
                + (sizeCheck.Layers.Count > 0 ? "layers: " + string.Join(", ", sizeCheck.Layers.ToArray()) : "all layers")
                + "): "
                + sizeCheck.Result);
            if (sizeCheck.DxfWidthMm.HasValue && sizeCheck.LinkWidthMm.HasValue)
            {
                lines.Add("  DXF:           " + FormatSize(sizeCheck.DxfWidthMm.Value, sizeCheck.DxfHeightMm ?? 0.0));
                lines.Add("  Link in Revit: " + FormatSize(sizeCheck.LinkWidthMm.Value, sizeCheck.LinkHeightMm ?? 0.0));
            }
            if (sizeCheck.Ratio.HasValue)
                lines.Add("  Ratio (Revit / DXF): " + sizeCheck.Ratio.Value.ToString("0.####", CultureInfo.InvariantCulture));
            if (sizeCheck.OffsetXMm.HasValue && sizeCheck.OffsetYMm.HasValue)
            {
                lines.Add("  Shift: X " + FormatDecimal(sizeCheck.OffsetXMm.Value)
                    + " mm, Y " + FormatDecimal(sizeCheck.OffsetYMm.Value) + " mm");
            }
            lines.Add("  " + sizeCheck.Message);
            if (sizeCheck.Result != LinkSizeCheckResult.Passed)
                lines.Add("  Click 08 Help for step-by-step instructions.");

            lines.Add(string.Empty);
            lines.Add("Saved: " + PathUtils.GetProjectStatePath());
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string FormatSize(double widthMm, double heightMm)
        {
            return widthMm.ToString("#,0", CultureInfo.InvariantCulture)
                + " x "
                + heightMm.ToString("#,0", CultureInfo.InvariantCulture)
                + " mm";
        }

        private static string FormatDecimal(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
