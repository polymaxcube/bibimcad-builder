using System;
using System.Collections.Generic;
using System.Globalization;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    // Carries review work (status, height, notes, Revit wall id) from the previous classification
    // to a new one, so re-running Classify does not throw away approvals.
    public class WallCandidateMerger
    {
        public const double GeometryToleranceMm = 0.5;

        public WallCandidateMergeResult Merge(
            IList<WallCandidate> previous,
            IList<WallCandidate> fresh,
            DateTime mergedAt)
        {
            if (fresh == null)
                throw new ArgumentNullException("fresh");

            WallCandidateMergeResult result = new WallCandidateMergeResult();
            List<WallCandidate> unmatched = previous == null ? new List<WallCandidate>() : new List<WallCandidate>(previous);
            Dictionary<string, WallCandidate> byId = new Dictionary<string, WallCandidate>(StringComparer.Ordinal);
            foreach (WallCandidate candidate in unmatched)
            {
                if (!string.IsNullOrEmpty(candidate.Id) && !byId.ContainsKey(candidate.Id))
                    byId[candidate.Id] = candidate;
            }

            foreach (WallCandidate candidate in fresh)
            {
                WallCandidate old;
                if (byId.TryGetValue(candidate.Id, out old) && unmatched.Contains(old))
                {
                    unmatched.Remove(old);
                    if (SameGeometry(old, candidate))
                    {
                        CopyReview(old, candidate);
                        result.KeptCount++;
                    }
                    else
                    {
                        candidate.HeightMm = old.HeightMm;
                        candidate.RevitElementId = old.RevitElementId;
                        candidate.Notes = AppendNote(
                            old.Notes,
                            "Geometry changed on reclassify "
                            + mergedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                            + "; review again.");
                        result.ResetCount++;
                    }

                    result.Candidates.Add(candidate);
                    continue;
                }

                // Entity handles can change when the DXF is exported again; fall back to geometry.
                WallCandidate sameShape = unmatched.Find(o => SameGeometry(o, candidate));
                if (sameShape != null)
                {
                    unmatched.Remove(sameShape);
                    CopyReview(sameShape, candidate);
                    result.KeptCount++;
                }

                result.Candidates.Add(candidate);
            }

            foreach (WallCandidate old in unmatched)
            {
                result.DroppedCount++;
                if (old.RevitElementId.HasValue)
                    result.DroppedWithWallIds.Add(old.RevitElementId.Value);
            }

            return result;
        }

        public static bool SameGeometry(WallCandidate a, WallCandidate b)
        {
            if (Math.Abs(a.ThicknessMm - b.ThicknessMm) > GeometryToleranceMm)
                return false;

            bool forward = Near(a.StartXMm, a.StartYMm, b.StartXMm, b.StartYMm)
                && Near(a.EndXMm, a.EndYMm, b.EndXMm, b.EndYMm);
            bool reverse = Near(a.StartXMm, a.StartYMm, b.EndXMm, b.EndYMm)
                && Near(a.EndXMm, a.EndYMm, b.StartXMm, b.StartYMm);
            return forward || reverse;
        }

        private static bool Near(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy) <= GeometryToleranceMm;
        }

        private static void CopyReview(WallCandidate from, WallCandidate to)
        {
            to.Status = from.Status;
            to.HeightMm = from.HeightMm;
            to.Notes = from.Notes;
            to.RevitElementId = from.RevitElementId;
        }

        private static string AppendNote(string existing, string note)
        {
            return string.IsNullOrWhiteSpace(existing) ? note : existing.Trim() + " " + note;
        }
    }

    public class WallCandidateMergeResult
    {
        public WallCandidateMergeResult()
        {
            Candidates = new List<WallCandidate>();
            DroppedWithWallIds = new List<long>();
        }

        public List<WallCandidate> Candidates { get; private set; }

        public int KeptCount { get; set; }

        public int ResetCount { get; set; }

        public int DroppedCount { get; set; }

        public List<long> DroppedWithWallIds { get; private set; }
    }
}
