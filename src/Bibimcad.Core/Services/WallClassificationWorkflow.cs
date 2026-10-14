using System;
using System.Collections.Generic;
using System.Globalization;
using Bibimcad.Models;
using netDxf;

namespace Bibimcad.Services
{
    public class WallClassificationWorkflow
    {
        private readonly WallClassificationOptions _options;

        public WallClassificationWorkflow()
            : this(new WallClassificationOptions())
        {
        }

        public WallClassificationWorkflow(WallClassificationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            _options = options;
        }

        // Problems that stop classification before the DXF is read. Empty when it may run.
        public List<string> GetBlockingProblems(ProjectState state)
        {
            List<string> problems = new List<string>();
            if (state == null || state.CadSource == null)
            {
                problems.Add("cadSource is missing from project_state.json.");
                return problems;
            }

            CadSourceInfo source = state.CadSource;
            if (string.IsNullOrWhiteSpace(source.DxfPath))
                problems.Add("cadSource.dxfPath is missing.");
            if (!source.UnitCalibrationApproved)
                problems.Add("cadSource.unitCalibrationApproved is false. Approve the unit calibration first.");
            if (source.MmPerCadUnit <= 0.0 || double.IsNaN(source.MmPerCadUnit) || double.IsInfinity(source.MmPerCadUnit))
                problems.Add("cadSource.mmPerCadUnit must be a positive number.");
            if (source.WallLayers == null || !source.WallLayers.Exists(l => !string.IsNullOrWhiteSpace(l)))
                problems.Add("cadSource.wallLayers is empty. List the DXF layers that hold wall edges.");

            return problems;
        }

        public WallClassificationSummary Run(ProjectState state, DxfDocument document, DateTime now)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            List<string> problems = GetBlockingProblems(state);
            if (problems.Count > 0)
                throw new InvalidOperationException(string.Join(" ", problems.ToArray()));

            state.UpgradeToCurrentSchema();
            CadSourceInfo source = state.CadSource;

            SegmentExtraction extraction = new CadSegmentExtractor().Extract(document, source.WallLayers, source.MmPerCadUnit);
            WallClassificationResult classification = new WallCandidateService().Classify(extraction.Segments, _options);
            WallCandidateMergeResult merge = new WallCandidateMerger().Merge(state.WallCandidates, classification.Candidates, now);

            WallClassificationInfo info = new WallClassificationInfo();
            info.ClassifiedAt = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            info.WallLayers.AddRange(source.WallLayers);
            info.SegmentCount = classification.SegmentCount;
            info.PairedSegmentCount = classification.PairedSegmentCount;
            info.UnpairedSegmentCount = classification.UnpairedSegmentCount;
            info.CandidateCount = merge.Candidates.Count;
            info.Warnings.AddRange(extraction.Warnings);
            info.Warnings.AddRange(classification.Warnings);
            if (merge.DroppedWithWallIds.Count > 0)
            {
                info.Warnings.Add(
                    merge.DroppedWithWallIds.Count.ToString(CultureInfo.InvariantCulture)
                    + " previous candidates with Revit walls no longer match the DXF and were removed. Revit element ids: "
                    + string.Join(", ", merge.DroppedWithWallIds.ConvertAll(id => id.ToString(CultureInfo.InvariantCulture)).ToArray())
                    + ". Check those walls in Revit.");
            }

            state.WallCandidates = merge.Candidates;
            state.WallClassification = info;

            WallClassificationSummary summary = new WallClassificationSummary();
            summary.Info = info;
            summary.KeptReviewCount = merge.KeptCount;
            summary.ResetReviewCount = merge.ResetCount;
            summary.DroppedCount = merge.DroppedCount;
            return summary;
        }
    }

    public class WallClassificationSummary
    {
        public WallClassificationInfo Info { get; set; }

        public int KeptReviewCount { get; set; }

        public int ResetReviewCount { get; set; }

        public int DroppedCount { get; set; }
    }
}
