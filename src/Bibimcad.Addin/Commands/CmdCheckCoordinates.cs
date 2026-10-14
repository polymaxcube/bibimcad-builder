using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Bibimcad.Models;
using Bibimcad.Services;
using Bibimcad.Utilities;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdCheckCoordinates : IExternalCommand
    {
        private const int MaxCommandLinks = 4;

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
                    ShowDialog("Open a project.", "Open a Revit project before checking coordinates.");
                    message = "No active Revit project.";
                    return Result.Failed;
                }

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                if (state == null || state.CadSource == null || string.IsNullOrWhiteSpace(state.CadSource.DwgName))
                {
                    ShowDialog("Project state is incomplete.", "CAD source DWG name is missing from project_state.json.");
                    message = "CAD source DWG name is missing.";
                    return Result.Failed;
                }

                string dwgName = state.CadSource.DwgName.Trim();
                ImportInstance importInstance;
                if (!TryResolveImportInstance(uiDocument, document, dwgName, out importInstance))
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
                ShowDialog(
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
                return Fail(ref message, "Coordinate check failed.", ex);
            }
        }

        private static bool TryResolveImportInstance(
            UIDocument uiDocument,
            Document document,
            string dwgName,
            out ImportInstance importInstance)
        {
            importInstance = null;
            List<ImportInstance> matches = FindLinkedMatches(document, dwgName);
            LoggingUtils.Info(
                "Linked CAD matches for '"
                + dwgName
                + "': "
                + matches.Count.ToString(CultureInfo.InvariantCulture)
                + ".");

            if (matches.Count == 1)
            {
                importInstance = matches[0];
                LoggingUtils.Info("Using the single matching CAD link " + Describe(document, importInstance) + ".");
                return true;
            }

            if (matches.Count > 1)
            {
                if (matches.Count <= MaxCommandLinks)
                    return TryChooseFromList(document, dwgName, matches, out importInstance);

                if (!Confirm(
                    "Several CAD links match.",
                    "More than four linked CAD files match " + dwgName + ". Select the correct ImportInstance."))
                {
                    return false;
                }

                importInstance = PickImportInstance(
                    uiDocument,
                    document,
                    "Select the linked CAD file named " + dwgName + ".");
                return true;
            }

            if (!Confirm(
                "No matching CAD link was found.",
                "Bibimcad looked for a linked CAD file named " + dwgName + ". Select an ImportInstance manually."))
            {
                return false;
            }

            importInstance = PickImportInstance(
                uiDocument,
                document,
                "Select a CAD import. No linked file named " + dwgName + " was found.");
            LoggingUtils.Info("Using manually selected CAD " + Describe(document, importInstance) + ".");
            return true;
        }

        private static List<ImportInstance> FindLinkedMatches(Document document, string dwgName)
        {
            List<ImportInstance> matches = new List<ImportInstance>();
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ImportInstance));

            foreach (Element element in collector)
            {
                ImportInstance importInstance = element as ImportInstance;
                if (importInstance == null || !importInstance.IsValidObject || !importInstance.IsLinked)
                    continue;

                string cadName = ElementNameUtils.GetCadName(document, importInstance);
                if (ElementNameUtils.NamesMatch(cadName, dwgName))
                    matches.Add(importInstance);
            }

            return matches;
        }

        private static bool TryChooseFromList(
            Document document,
            string dwgName,
            IList<ImportInstance> matches,
            out ImportInstance importInstance)
        {
            importInstance = null;
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = "Select the linked CAD file.";
            dialog.MainContent = "More than one linked CAD file matches " + dwgName + ".";
            dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
            dialog.AllowCancellation = true;

            for (int i = 0; i < matches.Count; i++)
            {
                TaskDialogCommandLinkId linkId = (TaskDialogCommandLinkId)((int)TaskDialogCommandLinkId.CommandLink1 + i);
                dialog.AddCommandLink(linkId, Describe(document, matches[i]));
            }

            TaskDialogResult dialogResult = dialog.Show();
            if (dialogResult == TaskDialogResult.Cancel || dialogResult == TaskDialogResult.Close)
                return false;

            int index = (int)dialogResult - (int)TaskDialogCommandLinkId.CommandLink1;
            if (index < 0 || index >= matches.Count)
                return false;

            importInstance = matches[index];
            LoggingUtils.Info("User selected CAD link " + Describe(document, importInstance) + ".");
            return true;
        }

        private static ImportInstance PickImportInstance(UIDocument uiDocument, Document document, string prompt)
        {
            Reference reference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new ImportInstanceSelectionFilter(),
                prompt);

            Element element = document.GetElement(reference);
            ImportInstance importInstance = element as ImportInstance;
            if (importInstance == null)
                throw new InvalidOperationException("The selected element is not a CAD import.");

            return importInstance;
        }

        private static bool Confirm(string instruction, string content)
        {
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = instruction;
            dialog.MainContent = content;
            dialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            dialog.AllowCancellation = true;
            return dialog.Show() == TaskDialogResult.Ok;
        }

        private static string Describe(Document document, ImportInstance importInstance)
        {
            string cadName = ElementNameUtils.GetCadName(document, importInstance);
            if (string.IsNullOrWhiteSpace(cadName))
                cadName = "(name unresolved)";

            return cadName
                + "  |  Id "
                + importInstance.Id.IntegerValue.ToString(CultureInfo.InvariantCulture);
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

        private static void ShowDialog(string instruction, string content)
        {
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = instruction;
            dialog.MainContent = content;
            dialog.Show();
        }

        private static Result Fail(ref string message, string summary, Exception ex)
        {
            try
            {
                LoggingUtils.Error(summary, ex);
            }
            catch (Exception logEx)
            {
                message = summary + " " + ex.Message + " Log write failed: " + logEx.Message;
                ShowDialog(summary, message);
                return Result.Failed;
            }

            message = summary + " See the Bibimcad log.";
            ShowDialog(summary, ex.Message);
            return Result.Failed;
        }

        private sealed class ImportInstanceSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is ImportInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
