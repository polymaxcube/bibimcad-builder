using System;

namespace Bibimcad.Models
{
    public static class WallCandidateStatus
    {
        public const string Unverified = "Unverified";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";

        public static readonly string[] All = new[] { Unverified, Approved, Rejected };

        public static bool IsApproved(WallCandidate candidate)
        {
            return candidate != null && string.Equals(candidate.Status, Approved, StringComparison.Ordinal);
        }
    }
}
