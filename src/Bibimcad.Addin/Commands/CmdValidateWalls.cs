using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public class CmdValidateWalls : IExternalCommand
    {
        private const int MaxFailuresShown = 10;

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
                    BibimcadDialog.Show("Open a project.", "Open a Revit project before validating walls.");
                    message = "No active Revit project.";
                    return Result.Failed;
                }

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                state.UpgradeToCurrentSchema();
                if (!state.WallCandidates.Any(WallCandidateStatus.IsApproved))
                {
                    BibimcadDialog.Show("No approved wall candidates.", "There is nothing to validate. Approve candidates in 05 Review.");
                    message = "No approved wall candidates.";
                    return Result.Cancelled;
                }

                if (state.CadSource == null || string.IsNullOrWhiteSpace(state.CadSource.DwgName))
                {
                    BibimcadDialog.Show("Project state is incomplete.", "cadSource.dwgName is missing from project_state.json.");
                    message = "CAD source DWG name is missing.";
                    return Result.Failed;
                }

                ImportInstance importInstance;
                if (!CadLinkPicker.TryResolve(uiDocument, document, state.CadSource.DwgName.Trim(), out importInstance))
                    return Result.Cancelled;

                Transform transform = importInstance.GetTotalTransform();
                if (transform == null)
                    throw new InvalidOperationException("ImportInstance.GetTotalTransform() returned no transform.");

                ValidationInfo validation = new WallValidationService().Validate(
                    document,
                    state.WallCandidates,
                    transform,
                    WallGeometryCheck.DefaultToleranceMm,
                    DateTime.Now);
                state.Validation = validation;
                repository.Save(state);

                LoggingUtils.Info(
                    "Validated walls: "
                    + validation.PassedCount.ToString(CultureInfo.InvariantCulture)
                    + " passed, "
                    + validation.FailedCount.ToString(CultureInfo.InvariantCulture)
                    + " failed.");
                BibimcadDialog.Show(
                    validation.FailedCount == 0 ? "All walls passed" : "Some walls failed validation",
                    BuildReport(validation));

                if (validation.FailedCount > 0)
                {
                    message = "Some walls failed validation.";
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
                return BibimcadDialog.Fail(ref message, "Validate failed.", ex);
            }
        }

        private static string BuildReport(ValidationInfo validation)
        {
            List<string> lines = new List<string>();
            lines.Add("Approved candidates checked: " + validation.CheckedCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Passed: " + validation.PassedCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Failed: " + validation.FailedCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Tolerance: " + validation.ToleranceMm.ToString("0.###", CultureInfo.InvariantCulture) + " mm");

            if (validation.Failures.Count > 0)
            {
                lines.Add(string.Empty);
                foreach (ValidationFailure failure in validation.Failures.Take(MaxFailuresShown))
                    lines.Add("  " + failure.CandidateId + ": " + failure.Reason);
                if (validation.Failures.Count > MaxFailuresShown)
                    lines.Add("  ... all failures are saved in project_state.json under validation.");
            }

            lines.Add(string.Empty);
            lines.Add("Saved: " + PathUtils.GetProjectStatePath());
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
