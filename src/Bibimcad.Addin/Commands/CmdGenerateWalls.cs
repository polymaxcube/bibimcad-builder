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
    [Transaction(TransactionMode.Manual)]
    public class CmdGenerateWalls : IExternalCommand
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
                    BibimcadDialog.Show("Open a project.", "Open a Revit project before generating walls.");
                    message = "No active Revit project.";
                    return Result.Failed;
                }

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                state.UpgradeToCurrentSchema();

                List<WallCandidate> approved = state.WallCandidates.Where(WallCandidateStatus.IsApproved).ToList();
                if (approved.Count == 0)
                {
                    BibimcadDialog.Show("No approved wall candidates.", "Approve candidates in 05 Review first.");
                    message = "No approved wall candidates.";
                    return Result.Cancelled;
                }

                if (state.CadSource == null || string.IsNullOrWhiteSpace(state.CadSource.DwgName))
                {
                    BibimcadDialog.Show("Project state is incomplete.", "cadSource.dwgName is missing from project_state.json.");
                    message = "CAD source DWG name is missing.";
                    return Result.Failed;
                }

                string levelProblem;
                Level level = WallGenerationService.ResolveLevel(
                    document,
                    state.Project == null ? null : state.Project.TargetLevelName,
                    document.ActiveView,
                    out levelProblem);
                if (level == null)
                {
                    BibimcadDialog.Show("No level for the walls.", levelProblem);
                    message = levelProblem;
                    return Result.Failed;
                }

                ImportInstance importInstance;
                if (!CadLinkPicker.TryResolve(uiDocument, document, state.CadSource.DwgName.Trim(), out importInstance))
                    return Result.Cancelled;

                Transform transform = importInstance.GetTotalTransform();
                if (transform == null)
                    throw new InvalidOperationException("ImportInstance.GetTotalTransform() returned no transform.");

                int existing = approved.Count(c => WallGenerationService.FindExistingWall(document, c) != null);
                if (!BibimcadDialog.Confirm(
                    "Create Revit walls?",
                    "Approved candidates: " + approved.Count.ToString(CultureInfo.InvariantCulture)
                        + Environment.NewLine + "Already in Revit (skipped): " + existing.ToString(CultureInfo.InvariantCulture)
                        + Environment.NewLine + "To create: " + (approved.Count - existing).ToString(CultureInfo.InvariantCulture)
                        + Environment.NewLine + "Level: " + level.Name
                        + Environment.NewLine + "CAD link: " + CadLinkLocator.Describe(document, importInstance)))
                {
                    return Result.Cancelled;
                }

                WallGenerationReport report = new WallGenerationService().Generate(document, state.WallCandidates, transform, level);
                repository.Save(state);

                LoggingUtils.Info("Generated walls on level " + level.Name + ": " + report.Summary() + ".");
                BibimcadDialog.Show(
                    report.Failures.Count == 0 ? "Walls created" : "Walls created with problems",
                    BuildReport(report, level));

                if (report.Created == 0 && report.Failures.Count > 0)
                {
                    message = "No walls were created.";
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
                return BibimcadDialog.Fail(ref message, "Generate walls failed.", ex);
            }
        }

        private static string BuildReport(WallGenerationReport report, Level level)
        {
            List<string> lines = new List<string>();
            lines.Add("Level: " + level.Name);
            lines.Add("Created: " + report.Created.ToString(CultureInfo.InvariantCulture));
            if (report.Recreated > 0)
                lines.Add("  of which replaced deleted walls: " + report.Recreated.ToString(CultureInfo.InvariantCulture));
            lines.Add("Skipped, already in Revit: " + report.SkippedExisting.ToString(CultureInfo.InvariantCulture));
            lines.Add("Failed: " + report.Failures.Count.ToString(CultureInfo.InvariantCulture));
            if (report.WallTypesCreated.Count > 0)
                lines.Add("New wall types: " + string.Join(", ", report.WallTypesCreated.ToArray()));
            if (report.SuppressedWarnings > 0)
                lines.Add("Revit warnings dismissed (overlaps, joins): " + report.SuppressedWarnings.ToString(CultureInfo.InvariantCulture));

            if (report.Failures.Count > 0)
            {
                lines.Add(string.Empty);
                foreach (string failure in report.Failures.Take(MaxFailuresShown))
                    lines.Add("  " + failure);
                if (report.Failures.Count > MaxFailuresShown)
                    lines.Add("  ... see the Bibimcad log for the rest.");
            }

            lines.Add(string.Empty);
            lines.Add("Next: 07 Validate.");
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
