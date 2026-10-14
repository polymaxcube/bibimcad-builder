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
using netDxf;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdClassifyWalls : IExternalCommand
    {
        private const int MaxLayersShown = 15;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                state.UpgradeToCurrentSchema();

                WallClassificationWorkflow workflow = new WallClassificationWorkflow();
                List<string> problems = workflow.GetBlockingProblems(state);
                if (problems.Count > 0)
                {
                    BibimcadDialog.Show("Classification cannot run.", BuildBlockedReport(state, problems));
                    message = "Classification cannot run.";
                    return Result.Failed;
                }

                string dxfPath = state.CadSource.DxfPath.Trim();
                DxfDocument document = DxfLoader.Load(dxfPath);
                WallClassificationSummary summary = workflow.Run(state, document, DateTime.Now);
                repository.Save(state);

                LoggingUtils.Info(
                    "Classified walls from "
                    + dxfPath
                    + ": "
                    + summary.Info.CandidateCount.ToString(CultureInfo.InvariantCulture)
                    + " candidates, "
                    + summary.Info.UnpairedSegmentCount.ToString(CultureInfo.InvariantCulture)
                    + " unpaired segments.");

                BibimcadDialog.Show("Wall candidates", BuildReport(state, summary));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return BibimcadDialog.Fail(ref message, "Classify failed.", ex);
            }
        }

        private static string BuildBlockedReport(ProjectState state, List<string> problems)
        {
            List<string> lines = new List<string>(problems);
            if (state.CadAudit != null && state.CadAudit.Layers != null && state.CadAudit.Layers.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Layers with the most entities (from 03 Read DXF):");
                foreach (CadLayerInfo layer in state.CadAudit.Layers
                    .Where(l => l.EntityCount > 0)
                    .OrderByDescending(l => l.EntityCount)
                    .Take(MaxLayersShown))
                {
                    lines.Add("  " + layer.Name + ": " + layer.EntityCount.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                lines.Add(string.Empty);
                lines.Add("Run 03 Read DXF to list the DXF layers.");
            }

            lines.Add(string.Empty);
            lines.Add("Edit " + PathUtils.GetProjectStatePath() + ", for example:");
            lines.Add("  \"wallLayers\": [\"A-WALL\"]");
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string BuildReport(ProjectState state, WallClassificationSummary summary)
        {
            WallClassificationInfo info = summary.Info;
            List<string> lines = new List<string>();
            lines.Add("Wall layers: " + string.Join(", ", info.WallLayers.ToArray()));
            lines.Add("Segments read: " + info.SegmentCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Paired segments: " + info.PairedSegmentCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Unpaired segments: " + info.UnpairedSegmentCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Wall candidates: " + info.CandidateCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);

            foreach (IGrouping<string, WallCandidate> group in state.WallCandidates.GroupBy(c => c.Status).OrderBy(g => g.Key))
                lines.Add(group.Key + ": " + group.Count().ToString(CultureInfo.InvariantCulture));

            if (summary.KeptReviewCount > 0 || summary.ResetReviewCount > 0 || summary.DroppedCount > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Previous review kept: " + summary.KeptReviewCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("Geometry changed, review again: " + summary.ResetReviewCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("Previous candidates removed: " + summary.DroppedCount.ToString(CultureInfo.InvariantCulture));
            }

            if (info.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Warnings:");
                foreach (string warning in info.Warnings)
                    lines.Add("  " + warning);
            }

            lines.Add(string.Empty);
            lines.Add("Nothing is approved automatically. Next: 05 Review.");
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
