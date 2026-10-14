using System.Collections.Generic;
using System.Linq;
using Bibimcad.Models;
using Bibimcad.Services;
using netDxf;
using Xunit;

namespace Bibimcad.Tests
{
    public class WallCandidateServiceTests
    {
        private static WallClassificationResult Classify(params CadSegment[] segments)
        {
            return new WallCandidateService().Classify(segments, new WallClassificationOptions());
        }

        private static CadSegment Seg(string id, double x1, double y1, double x2, double y2)
        {
            return new CadSegment(id, "A-WALL", x1, y1, x2, y2);
        }

        [Theory]
        [InlineData(74.9, false)]
        [InlineData(75.0, true)]
        [InlineData(350.0, true)]
        [InlineData(350.1, false)]
        public void Classify_PairsOnlyWithinTheGapRange(double gap, bool paired)
        {
            WallClassificationResult result = Classify(Seg("a", 0, 0, 5000, 0), Seg("b", 0, gap, 5000, gap));

            Assert.Equal(paired ? 1 : 0, result.Candidates.Count);
        }

        [Theory]
        [InlineData(3300.0, true)]
        [InlineData(3310.0, false)]
        public void Classify_RequiresSeventyPercentOverlapOfTheShorterSegment(double shortStart, bool paired)
        {
            // The 1000 mm segment overlaps the 4000 mm one by 4000 - shortStart.
            WallClassificationResult result = Classify(
                Seg("long", 0, 0, 4000, 0),
                Seg("short", shortStart, 200, shortStart + 1000, 200));

            Assert.Equal(paired ? 1 : 0, result.Candidates.Count);
        }

        [Fact]
        public void Classify_BuildsCenterlineAndThicknessFromThePair()
        {
            WallCandidate wall = Classify(Seg("a", 6000, 0, 0, 0), Seg("b", 0, 200, 6000, 200)).Candidates.Single();

            Assert.Equal(0.0, wall.StartXMm, 3);
            Assert.Equal(100.0, wall.StartYMm, 3);
            Assert.Equal(6000.0, wall.EndXMm, 3);
            Assert.Equal(100.0, wall.EndYMm, 3);
            Assert.Equal(6000.0, wall.LengthMm, 3);
            Assert.Equal(200.0, wall.ThicknessMm, 3);
            Assert.Equal("horizontal", wall.Orientation);
            Assert.Equal(WallCandidateStatus.Unverified, wall.Status);
            Assert.Equal("W-a+b", wall.Id);
        }

        [Fact]
        public void Classify_UsesOnlyTheOverlappingPartForTheCenterline()
        {
            WallCandidate wall = Classify(Seg("a", 0, 0, 5000, 0), Seg("b", 1000, 200, 5000, 200)).Candidates.Single();

            Assert.Equal(1000.0, wall.StartXMm, 3);
            Assert.Equal(5000.0, wall.EndXMm, 3);
            Assert.Equal(4000.0, wall.LengthMm, 3);
        }

        [Fact]
        public void Classify_UsesEachSegmentInOnePairOnly()
        {
            WallClassificationResult result = Classify(
                Seg("a", 0, 0, 5000, 0),
                Seg("b", 0, 200, 5000, 200),
                Seg("c", 0, 400, 5000, 400));

            Assert.Single(result.Candidates);
            Assert.Equal(2, result.PairedSegmentCount);
            Assert.Equal(1, result.UnpairedSegmentCount);
        }

        [Fact]
        public void Classify_IgnoresSegmentsMoreThanOneDegreeFromParallel()
        {
            double rise = 5000 * System.Math.Tan(2.0 * System.Math.PI / 180.0);

            WallClassificationResult result = Classify(Seg("a", 0, 0, 5000, 0), Seg("b", 0, 200, 5000, 200 + rise));

            Assert.Empty(result.Candidates);
        }

        [Fact]
        public void Classify_PairsSegmentsAcrossTheZeroAndOneEightyDegreeWrap()
        {
            double drop = 5000 * System.Math.Tan(0.3 * System.Math.PI / 180.0);

            WallClassificationResult result = Classify(Seg("a", 0, 0, 5000, drop), Seg("b", 0, 200, 5000, 200 - drop));

            Assert.Single(result.Candidates);
        }

        [Fact]
        public void Classify_PairsAngledWalls()
        {
            double offset = 200 / System.Math.Sqrt(2);

            WallCandidate wall = Classify(
                Seg("a", 0, 0, 2000, 2000),
                Seg("b", -offset, offset, 2000 - offset, 2000 + offset)).Candidates.Single();

            Assert.Equal("angled", wall.Orientation);
            Assert.Equal(200.0, wall.ThicknessMm, 3);
            Assert.Equal(2828.427, wall.LengthMm, 3);
        }

        [Fact]
        public void SampleDxf_ProducesTheThreeKnownWalls()
        {
            DxfDocument document = DxfLoader.Load(TestPaths.SampleDxfPath);
            SegmentExtraction extraction = new CadSegmentExtractor().Extract(document, new[] { "A-WALL" }, 1.0);

            WallClassificationResult result = new WallCandidateService().Classify(extraction.Segments, new WallClassificationOptions());

            Assert.Equal(10, extraction.Segments.Count);
            Assert.Equal(1, extraction.ArcSegmentCount);
            Assert.Equal(3, result.Candidates.Count);
            Assert.Equal(6, result.PairedSegmentCount);
            Assert.Equal(4, result.UnpairedSegmentCount);

            WallCandidate horizontal = result.Candidates.Single(c => c.Orientation == "horizontal");
            Assert.Equal(200.0, horizontal.ThicknessMm, 3);
            Assert.Equal(100.0, horizontal.StartYMm, 3);
            Assert.Equal(6000.0, horizontal.LengthMm, 3);

            WallCandidate vertical = result.Candidates.Single(c => c.Orientation == "vertical");
            Assert.Equal(150.0, vertical.ThicknessMm, 3);
            Assert.Equal(6075.0, vertical.StartXMm, 3);
            Assert.Equal(1000.0, vertical.StartYMm, 3);
            Assert.Equal(4000.0, vertical.EndYMm, 3);

            WallCandidate angled = result.Candidates.Single(c => c.Orientation == "angled");
            Assert.Equal(-70.711, angled.StartXMm, 3);
            Assert.Equal(6070.711, angled.StartYMm, 3);
        }

        [Fact]
        public void Extract_IgnoresLayersThatAreNotWallLayers()
        {
            DxfDocument document = DxfLoader.Load(TestPaths.SampleDxfPath);

            SegmentExtraction extraction = new CadSegmentExtractor().Extract(document, new[] { "A-WALL" }, 1.0);

            Assert.DoesNotContain(extraction.Segments, s => s.Layer == "A-ANNO");
        }

        [Fact]
        public void Extract_ConvertsCadUnitsToMillimeters()
        {
            DxfDocument document = DxfLoader.Load(TestPaths.SampleDxfPath);

            SegmentExtraction extraction = new CadSegmentExtractor().Extract(document, new[] { "A-ANNO" }, 25.4);

            CadSegment annotation = extraction.Segments.Single();
            Assert.Equal(-100 * 25.4, annotation.Y1, 6);
            Assert.Equal(6000 * 25.4, System.Math.Max(annotation.X1, annotation.X2), 6);
        }

        [Fact]
        public void Extract_WarnsForWallLayersWithNoSegments()
        {
            DxfDocument document = DxfLoader.Load(TestPaths.SampleDxfPath);

            SegmentExtraction extraction = new CadSegmentExtractor().Extract(document, new[] { "A-WALL", "NO-SUCH-LAYER" }, 1.0);

            Assert.Contains(extraction.Warnings, w => w.Contains("NO-SUCH-LAYER"));
        }
    }
}
