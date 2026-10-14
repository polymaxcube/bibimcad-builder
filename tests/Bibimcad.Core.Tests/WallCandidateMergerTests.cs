using System;
using System.Collections.Generic;
using Bibimcad.Models;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class WallCandidateMergerTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 14, 11, 0, 0);

        private static WallCandidate Wall(string id, double endX, double thickness = 200)
        {
            return new WallCandidate
            {
                Id = id,
                Layer = "A-WALL",
                StartXMm = 0,
                StartYMm = 100,
                EndXMm = endX,
                EndYMm = 100,
                LengthMm = endX,
                ThicknessMm = thickness
            };
        }

        private static WallCandidate Reviewed(string id, double endX)
        {
            WallCandidate wall = Wall(id, endX);
            wall.Status = WallCandidateStatus.Approved;
            wall.HeightMm = 2800;
            wall.Notes = "ok";
            wall.RevitElementId = 501;
            return wall;
        }

        [Fact]
        public void Merge_KeepsReviewWhenIdAndGeometryMatch()
        {
            WallCandidateMergeResult result = new WallCandidateMerger().Merge(
                new List<WallCandidate> { Reviewed("W-1+2", 6000) },
                new List<WallCandidate> { Wall("W-1+2", 6000.3) },
                Now);

            WallCandidate merged = result.Candidates[0];
            Assert.Equal(WallCandidateStatus.Approved, merged.Status);
            Assert.Equal(2800, merged.HeightMm);
            Assert.Equal(501, merged.RevitElementId);
            Assert.Equal(1, result.KeptCount);
        }

        [Fact]
        public void Merge_ResetsStatusWhenGeometryChanged()
        {
            WallCandidateMergeResult result = new WallCandidateMerger().Merge(
                new List<WallCandidate> { Reviewed("W-1+2", 6000) },
                new List<WallCandidate> { Wall("W-1+2", 5000) },
                Now);

            WallCandidate merged = result.Candidates[0];
            Assert.Equal(WallCandidateStatus.Unverified, merged.Status);
            Assert.Equal(501, merged.RevitElementId);
            Assert.Contains("Geometry changed", merged.Notes);
            Assert.Equal(1, result.ResetCount);
        }

        [Fact]
        public void Merge_MatchesByGeometryWhenHandlesChanged()
        {
            WallCandidateMergeResult result = new WallCandidateMerger().Merge(
                new List<WallCandidate> { Reviewed("W-1+2", 6000) },
                new List<WallCandidate> { Wall("W-9+10", 6000) },
                Now);

            Assert.Equal(WallCandidateStatus.Approved, result.Candidates[0].Status);
            Assert.Equal("W-9+10", result.Candidates[0].Id);
            Assert.Equal(0, result.DroppedCount);
        }

        [Fact]
        public void Merge_ReportsDroppedCandidatesThatHadWalls()
        {
            WallCandidateMergeResult result = new WallCandidateMerger().Merge(
                new List<WallCandidate> { Reviewed("W-1+2", 6000) },
                new List<WallCandidate> { Wall("W-3+4", 3000) },
                Now);

            Assert.Equal(1, result.DroppedCount);
            Assert.Equal(new long[] { 501 }, result.DroppedWithWallIds);
            Assert.Equal(WallCandidateStatus.Unverified, result.Candidates[0].Status);
        }

        [Fact]
        public void SameGeometry_AcceptsReversedEndpoints()
        {
            WallCandidate a = Wall("a", 6000);
            WallCandidate b = Wall("b", 6000);
            b.StartXMm = 6000;
            b.EndXMm = 0;

            Assert.True(WallCandidateMerger.SameGeometry(a, b));
        }
    }
}
