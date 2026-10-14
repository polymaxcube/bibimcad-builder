using System;
using System.Collections.Generic;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    // Review rules shared by the review window and the CSV import.
    // A candidate can only be Approved when it has a positive height.
    public class WallReviewService
    {
        private readonly double? _defaultHeightMm;

        public WallReviewService(double? defaultHeightMm)
        {
            _defaultHeightMm = IsValidHeight(defaultHeightMm) ? defaultHeightMm : null;
        }

        public double? DefaultHeightMm
        {
            get { return _defaultHeightMm; }
        }

        public static bool IsValidHeight(double? heightMm)
        {
            return heightMm.HasValue
                && heightMm.Value > 0.0
                && !double.IsNaN(heightMm.Value)
                && !double.IsInfinity(heightMm.Value);
        }

        // Returns the candidates that stayed unapproved because they have no height and there is no default.
        public List<WallCandidate> Approve(IEnumerable<WallCandidate> candidates)
        {
            List<WallCandidate> skipped = new List<WallCandidate>();
            foreach (WallCandidate candidate in candidates)
            {
                if (!TryApprove(candidate))
                    skipped.Add(candidate);
            }

            return skipped;
        }

        public bool TryApprove(WallCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException("candidate");

            if (!IsValidHeight(candidate.HeightMm))
            {
                if (_defaultHeightMm == null)
                    return false;

                candidate.HeightMm = _defaultHeightMm;
            }

            candidate.Status = WallCandidateStatus.Approved;
            return true;
        }

        public void Reject(IEnumerable<WallCandidate> candidates)
        {
            SetStatus(candidates, WallCandidateStatus.Rejected);
        }

        public void MarkUnverified(IEnumerable<WallCandidate> candidates)
        {
            SetStatus(candidates, WallCandidateStatus.Unverified);
        }

        public void SetHeight(IEnumerable<WallCandidate> candidates, double heightMm)
        {
            if (!IsValidHeight(heightMm))
                throw new ArgumentOutOfRangeException("heightMm", "Wall height must be a positive number of millimeters.");

            foreach (WallCandidate candidate in candidates)
                candidate.HeightMm = heightMm;
        }

        public static List<WallCandidate> ApprovedWithoutHeight(IEnumerable<WallCandidate> candidates)
        {
            List<WallCandidate> result = new List<WallCandidate>();
            foreach (WallCandidate candidate in candidates)
            {
                if (WallCandidateStatus.IsApproved(candidate) && !IsValidHeight(candidate.HeightMm))
                    result.Add(candidate);
            }

            return result;
        }

        // Copies status, height, and notes from edited copies back onto the stored candidates by id.
        public static int ApplyReview(IList<WallCandidate> target, IEnumerable<WallCandidate> edited)
        {
            Dictionary<string, WallCandidate> byId = new Dictionary<string, WallCandidate>(StringComparer.Ordinal);
            foreach (WallCandidate candidate in target)
            {
                if (!string.IsNullOrEmpty(candidate.Id))
                    byId[candidate.Id] = candidate;
            }

            int changed = 0;
            foreach (WallCandidate copy in edited)
            {
                WallCandidate original;
                if (copy == null || string.IsNullOrEmpty(copy.Id) || !byId.TryGetValue(copy.Id, out original))
                    continue;

                if (!string.Equals(original.Status, copy.Status, StringComparison.Ordinal)
                    || original.HeightMm != copy.HeightMm
                    || !string.Equals(original.Notes ?? string.Empty, copy.Notes ?? string.Empty, StringComparison.Ordinal))
                {
                    changed++;
                }

                original.Status = copy.Status;
                original.HeightMm = copy.HeightMm;
                original.Notes = copy.Notes;
            }

            return changed;
        }

        private static void SetStatus(IEnumerable<WallCandidate> candidates, string status)
        {
            foreach (WallCandidate candidate in candidates)
                candidate.Status = status;
        }
    }
}
