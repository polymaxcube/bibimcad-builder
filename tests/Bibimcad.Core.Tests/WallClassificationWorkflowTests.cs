using System;
using System.Collections.Generic;
using System.Linq;
using Bibimcad.Models;
using Bibimcad.Services;
using netDxf;
using Xunit;

namespace Bibimcad.Tests
{
    public class WallClassificationWorkflowTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 14, 10, 0, 0);

        private static ProjectState SampleState()
        {
            ProjectState state = new JsonProjectRepository(TestPaths.TemplateStatePath).Load();
            state.CadSource.DxfPath = TestPaths.SampleDxfPath;
            state.CadSource.WallLayers = new List<string> { "A-WALL" };
            return state;
        }

        private static DxfDocument Sample()
        {
            return DxfLoader.Load(TestPaths.SampleDxfPath);
        }

        [Fact]
        public void Run_StoresCandidatesAndSummary()
        {
            ProjectState state = SampleState();

            WallClassificationSummary summary = new WallClassificationWorkflow().Run(state, Sample(), Now);

            Assert.Equal(ProjectState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.Equal(3, state.WallCandidates.Count);
            Assert.Equal(3, summary.Info.CandidateCount);
            Assert.Equal(10, summary.Info.SegmentCount);
            Assert.Equal(4, summary.Info.UnpairedSegmentCount);
            Assert.Equal("2026-10-14T10:00:00", summary.Info.ClassifiedAt);
            Assert.Contains(summary.Info.Warnings, w => w.Contains("curved polyline"));
            Assert.Contains(summary.Info.Warnings, w => w.Contains("no parallel partner"));
            Assert.All(state.WallCandidates, c => Assert.Equal(WallCandidateStatus.Unverified, c.Status));
        }

        [Fact]
        public void Run_KeepsReviewWorkWhenGeometryIsUnchanged()
        {
            ProjectState state = SampleState();
            WallClassificationWorkflow workflow = new WallClassificationWorkflow();
            workflow.Run(state, Sample(), Now);
            WallCandidate approved = state.WallCandidates[0];
            approved.Status = WallCandidateStatus.Approved;
            approved.HeightMm = 3000;
            approved.Notes = "checked";
            approved.RevitElementId = 12345;

            WallClassificationSummary summary = workflow.Run(state, Sample(), Now.AddHours(1));

            WallCandidate again = state.WallCandidates.Single(c => c.Id == approved.Id);
            Assert.Equal(WallCandidateStatus.Approved, again.Status);
            Assert.Equal(3000, again.HeightMm);
            Assert.Equal("checked", again.Notes);
            Assert.Equal(12345, again.RevitElementId);
            Assert.Equal(3, summary.KeptReviewCount);
        }

        [Fact]
        public void Run_BlocksWhenCalibrationIsNotApproved()
        {
            ProjectState state = SampleState();
            state.CadSource.UnitCalibrationApproved = false;
            WallClassificationWorkflow workflow = new WallClassificationWorkflow();

            Assert.Contains(workflow.GetBlockingProblems(state), p => p.Contains("unitCalibrationApproved"));
            Assert.Throws<InvalidOperationException>(() => workflow.Run(state, Sample(), Now));
        }

        [Fact]
        public void Run_BlocksWhenNoWallLayersAreListed()
        {
            ProjectState state = SampleState();
            state.CadSource.WallLayers = new List<string>();

            Assert.Contains(new WallClassificationWorkflow().GetBlockingProblems(state), p => p.Contains("wallLayers"));
        }
    }
}
