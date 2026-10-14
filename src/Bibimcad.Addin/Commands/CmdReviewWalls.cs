using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Interop;
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
    public class CmdReviewWalls : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                state.UpgradeToCurrentSchema();
                if (state.WallCandidates.Count == 0)
                {
                    BibimcadDialog.Show("No wall candidates.", "Run 04 Classify first.");
                    message = "No wall candidates.";
                    return Result.Cancelled;
                }

                double? defaultHeight = state.Project == null ? null : state.Project.DefaultWallHeightMm;
                WallReviewWindow window = new WallReviewWindow(state.WallCandidates, defaultHeight, PathUtils.GetWallReviewCsvPath());
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                int changed = WallReviewService.ApplyReview(state.WallCandidates, window.EditedCandidates);
                repository.Save(state);

                LoggingUtils.Info("Saved wall review: " + changed.ToString(CultureInfo.InvariantCulture) + " candidates changed.");
                BibimcadDialog.Show("Wall review saved", BuildReport(state, changed));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return BibimcadDialog.Fail(ref message, "Review failed.", ex);
            }
        }

        private static string BuildReport(ProjectState state, int changed)
        {
            List<string> lines = new List<string>();
            lines.Add("Changed candidates: " + changed.ToString(CultureInfo.InvariantCulture));
            foreach (string status in WallCandidateStatus.All)
            {
                int count = state.WallCandidates.Count(c => string.Equals(c.Status, status, StringComparison.Ordinal));
                lines.Add(status + ": " + count.ToString(CultureInfo.InvariantCulture));
            }

            lines.Add(string.Empty);
            lines.Add("Next: 06 Generate creates Revit walls for Approved candidates.");
            lines.Add("Saved: " + PathUtils.GetProjectStatePath());
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
