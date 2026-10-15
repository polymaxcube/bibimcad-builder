using System.Collections.Generic;
using System.Linq;
using Bibimcad.Models;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class HelpAdvisorTests
    {
        private static ProjectState HealthyState()
        {
            ProjectState state = new ProjectState();
            state.SchemaVersion = ProjectState.CurrentSchemaVersion;
            state.Project = new ProjectInfo();
            state.CadSource = new CadSourceInfo
            {
                DxfPath = @"C:\Bibimcad\input\K1.dxf",
                DwgName = "K1.dwg",
                MmPerCadUnit = 1.0,
                UnitCalibrationApproved = true,
            };
            state.CadSource.WallLayers.Add("HV-WALL");
            state.CadAudit = new CadAuditInfo { ReadAt = "2026-10-15T10:00:00" };
            state.CadAudit.EntityCounts["LINE"] = 4720;
            state.CadAudit.Layers.Add(new CadLayerInfo { Name = "HV-WALL", EntityCount = 4720 });
            state.RevitLink = new RevitLinkInfo
            {
                Transform = new CadTransformInfo(),
                SizeCheck = new LinkSizeCheckInfo { Result = LinkSizeCheckResult.Passed },
            };
            state.WallClassification = new WallClassificationInfo { CandidateCount = 1 };
            state.WallCandidates.Add(new WallCandidate
            {
                Id = "W-1+2",
                Status = WallCandidateStatus.Approved,
                HeightMm = 3000,
                RevitElementId = 123,
            });
            return state;
        }

        private static List<HelpTopic> Diagnose(ProjectState state)
        {
            return HelpAdvisor.Diagnose(state, path => true);
        }

        [Fact]
        public void Diagnose_ReportsNoProblemsForAHealthyProject()
        {
            List<HelpTopic> topics = Diagnose(HealthyState());

            HelpTopic only = Assert.Single(topics);
            Assert.Equal(HelpSeverity.Ok, only.Severity);
        }

        [Fact]
        public void Diagnose_ExplainsRelinkWhenLinkIsTooBig()
        {
            ProjectState state = HealthyState();
            state.RevitLink.SizeCheck = new LinkSizeCheckInfo
            {
                Result = LinkSizeCheckResult.ScaleMismatch,
                Ratio = 25.4,
                LikelyImportUnit = "Inch",
                Message = "Revit draws the link 25.4 times bigger than the DXF.",
                DxfWidthMm = 60000,
                DxfHeightMm = 30000,
                LinkWidthMm = 1524000,
                LinkHeightMm = 762000,
            };

            HelpTopic topic = Diagnose(state).First();

            Assert.Equal(HelpSeverity.Problem, topic.Severity);
            Assert.Equal("Relink K1.dwg in millimeters", topic.Title);
            Assert.Contains(topic.Steps, s => s.Contains("Import units = Millimeter"));
            Assert.Contains("1,524,000", topic.Detail);
        }

        [Fact]
        public void Diagnose_AsksToRerunCoordinatesWhenSizeWasNeverChecked()
        {
            ProjectState state = HealthyState();
            state.RevitLink.SizeCheck = null;

            Assert.Contains(Diagnose(state), t => t.Title == "The link size has not been checked");
        }

        [Fact]
        public void Diagnose_ExplainsADrawingInsideABlock()
        {
            ProjectState state = HealthyState();
            state.CadAudit.EntityCounts.Clear();
            state.CadAudit.EntityCounts["INSERT"] = 1;

            Assert.Contains(Diagnose(state), t => t.Title == "The drawing is inside a block");
        }

        [Fact]
        public void Diagnose_ListsBusiestLayersWhenNoWallLayers()
        {
            ProjectState state = HealthyState();
            state.CadSource.WallLayers.Clear();

            HelpTopic topic = Diagnose(state).Single(t => t.Title == "No wall layers chosen");
            Assert.Contains("HV-WALL (4720)", topic.Detail);
        }

        [Fact]
        public void Diagnose_FlagsMisspelledWallLayer()
        {
            ProjectState state = HealthyState();
            state.CadSource.WallLayers[0] = "HV_WALL";

            Assert.Contains(Diagnose(state), t => t.Title == "Wall layer name not found in the DXF");
        }

        [Fact]
        public void Diagnose_FlagsMissingDxfAndApprovedWallsWithoutHeight()
        {
            ProjectState state = HealthyState();
            state.WallCandidates[0].HeightMm = null;

            List<HelpTopic> topics = HelpAdvisor.Diagnose(state, path => false);

            Assert.Contains(topics, t => t.Title == "The DXF file is missing");
            Assert.Contains(topics, t => t.Title == "Approved walls without a height");
        }

        [Fact]
        public void Diagnose_PointsValidationFailuresAtTheLinkWhenItFailed()
        {
            ProjectState state = HealthyState();
            state.RevitLink.SizeCheck = new LinkSizeCheckInfo { Result = LinkSizeCheckResult.Offset, Message = "shifted" };
            state.Validation = new ValidationInfo { FailedCount = 1 };
            state.Validation.Failures.Add(new ValidationFailure { CandidateId = "W-1+2", Reason = "Endpoint is 9000 mm from the mapped point." });

            HelpTopic topic = Diagnose(state).Single(t => t.Title.StartsWith("07 Validate found"));
            Assert.Contains("fix the link first", topic.Detail);
        }

        [Fact]
        public void CommonProblems_AllHaveSteps()
        {
            Assert.All(HelpAdvisor.CommonProblems(), t => Assert.NotEmpty(t.Steps));
        }
    }
}
