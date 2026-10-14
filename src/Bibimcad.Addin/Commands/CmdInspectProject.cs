using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bibimcad.Models;
using Bibimcad.Services;
using Bibimcad.Utilities;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdInspectProject : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                List<string> errors = Validate(state);
                if (errors.Count > 0)
                {
                    string details = string.Join(Environment.NewLine, errors.ToArray());
                    LoggingUtils.Error("Project state validation failed. " + details, null);
                    ShowDialog("Project state is incomplete.", details);
                    message = "Project state is incomplete.";
                    return Result.Failed;
                }

                ShowDialog("Project state", BuildReport(state));
                LoggingUtils.Info("Inspected project '" + state.Project.ProjectName + "'.");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return Fail(ref message, "Inspect failed.", ex);
            }
        }

        private static List<string> Validate(ProjectState state)
        {
            List<string> errors = new List<string>();
            if (state == null)
            {
                errors.Add("Project state is missing.");
                return errors;
            }

            if (!string.Equals(state.SchemaVersion, ProjectState.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                errors.Add("Schema version must be " + ProjectState.CurrentSchemaVersion + ".");
            }

            if (state.Project == null)
            {
                errors.Add("Project info is missing.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(state.Project.SystemName))
                    errors.Add("System name is missing.");
                if (string.IsNullOrWhiteSpace(state.Project.ProjectName))
                    errors.Add("Project name is missing.");
                if (state.Project.TargetRevitVersion <= 0)
                    errors.Add("Target Revit version is missing.");
                if (state.Project.TargetLod <= 0)
                    errors.Add("Target LOD is missing.");
            }

            if (state.CadSource == null)
            {
                errors.Add("CAD source info is missing.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(state.CadSource.DxfPath))
                    errors.Add("DXF path is missing.");
                if (string.IsNullOrWhiteSpace(state.CadSource.DwgName))
                    errors.Add("DWG name is missing.");
                if (string.IsNullOrWhiteSpace(state.CadSource.ApprovedCoordinateUnit))
                    errors.Add("Approved coordinate unit is missing.");
            }

            return errors;
        }

        private static string BuildReport(ProjectState state)
        {
            List<string> lines = new List<string>();
            lines.Add("System name: " + state.Project.SystemName);
            lines.Add("Project name: " + state.Project.ProjectName);
            lines.Add("Target Revit version: " + state.Project.TargetRevitVersion.ToString(CultureInfo.InvariantCulture));
            lines.Add("LOD: " + state.Project.TargetLod.ToString(CultureInfo.InvariantCulture));
            lines.Add("DXF path: " + state.CadSource.DxfPath);
            lines.Add("CAD approved unit: " + state.CadSource.ApprovedCoordinateUnit);
            lines.Add("Unit calibration approved: " + (state.CadSource.UnitCalibrationApproved ? "Yes" : "No"));
            return string.Join(Environment.NewLine, lines.ToArray());
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
    }
}
