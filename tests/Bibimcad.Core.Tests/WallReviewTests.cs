using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bibimcad.Models;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class WallReviewTests : IDisposable
    {
        private readonly string _directory;

        public WallReviewTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "BibimcadReview_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private static List<WallCandidate> Candidates()
        {
            return new List<WallCandidate>
            {
                new WallCandidate { Id = "W-1+2", Layer = "A-WALL", EndXMm = 6000, LengthMm = 6000, ThicknessMm = 200, Orientation = "horizontal", SourceSegmentIds = new List<string> { "1", "2" } },
                new WallCandidate { Id = "W-3+4", Layer = "A-WALL", EndYMm = 3000, LengthMm = 3000, ThicknessMm = 150, Orientation = "vertical", Notes = "has, comma \"and quotes\"" },
                new WallCandidate { Id = "W-5+6", Layer = "A-WALL", EndXMm = 2000, LengthMm = 2000, ThicknessMm = 100, Orientation = "horizontal" }
            };
        }

        [Fact]
        public void Approve_UsesDefaultHeightWhenMissing()
        {
            WallCandidate wall = Candidates()[0];

            List<WallCandidate> skipped = new WallReviewService(3000).Approve(new[] { wall });

            Assert.Empty(skipped);
            Assert.Equal(WallCandidateStatus.Approved, wall.Status);
            Assert.Equal(3000, wall.HeightMm);
        }

        [Fact]
        public void Approve_SkipsCandidatesWithoutHeightWhenThereIsNoDefault()
        {
            WallCandidate wall = Candidates()[0];

            List<WallCandidate> skipped = new WallReviewService(null).Approve(new[] { wall });

            Assert.Single(skipped);
            Assert.Equal(WallCandidateStatus.Unverified, wall.Status);
        }

        [Fact]
        public void SetHeight_RejectsNonPositiveValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallReviewService(null).SetHeight(Candidates(), 0));
        }

        [Fact]
        public void ApplyReview_CopiesOnlyReviewFieldsById()
        {
            List<WallCandidate> stored = Candidates();
            WallCandidate edited = stored[1].Clone();
            edited.Status = WallCandidateStatus.Rejected;
            edited.ThicknessMm = 999;

            int changed = WallReviewService.ApplyReview(stored, new[] { edited });

            Assert.Equal(1, changed);
            Assert.Equal(WallCandidateStatus.Rejected, stored[1].Status);
            Assert.Equal(150, stored[1].ThicknessMm);
        }

        [Fact]
        public void Csv_RoundTripsStatusHeightAndNotes()
        {
            List<WallCandidate> source = Candidates();
            source[0].Status = WallCandidateStatus.Approved;
            source[0].HeightMm = 2800;
            source[1].Status = WallCandidateStatus.Rejected;
            string path = Path.Combine(_directory, "wall_review.csv");
            WallReviewCsv csv = new WallReviewCsv();
            csv.Export(source, path);

            List<WallCandidate> target = Candidates();
            WallReviewCsvImport result = csv.Import(path, target, new WallReviewService(null));

            Assert.Equal(3, result.UpdatedCount);
            Assert.Empty(result.Errors);
            Assert.Equal(WallCandidateStatus.Approved, target[0].Status);
            Assert.Equal(2800, target[0].HeightMm);
            Assert.Equal(WallCandidateStatus.Rejected, target[1].Status);
            Assert.Equal("has, comma \"and quotes\"", target[1].Notes);
            Assert.Equal(WallCandidateStatus.Unverified, target[2].Status);
        }

        [Fact]
        public void Csv_ExportUsesThePythonToolColumns()
        {
            string path = Path.Combine(_directory, "wall_review.csv");

            new WallReviewCsv().Export(Candidates(), path);

            string header = File.ReadAllLines(path, Encoding.UTF8)[0].TrimStart('\uFEFF');
            Assert.Equal(
                "id,layer,x1,y1,x2,y2,length_cad,orientation,possible_parallel_edge,possible_gap_cad,wall_candidate,approve,wall_height_mm,notes",
                header);
            Assert.Equal(0xEF, File.ReadAllBytes(path)[0]);
        }

        [Fact]
        public void Csv_ImportReadsSemicolonFilesAndReportsProblems()
        {
            string path = Path.Combine(_directory, "excel.csv");
            File.WriteAllText(
                path,
                "id;approve;wall_height_mm;notes\r\n"
                + "W-1+2;YES;;needs height\r\n"
                + "W-3+4;MAYBE;3000;\r\n"
                + "W-5+6;YES;-5;\r\n"
                + "W-9+9;YES;3000;\r\n",
                new UTF8Encoding(true));
            List<WallCandidate> target = Candidates();

            WallReviewCsvImport result = new WallReviewCsv().Import(path, target, new WallReviewService(null));

            Assert.Equal(new[] { "W-1+2" }, result.ApprovalsWithoutHeight);
            Assert.Equal(WallCandidateStatus.Unverified, target[0].Status);
            Assert.Equal("needs height", target[0].Notes);
            Assert.Equal(2, result.Errors.Count);
            Assert.Null(target[1].HeightMm);
            Assert.Equal(new[] { "W-9+9" }, result.UnknownIds);
        }

        [Fact]
        public void Csv_ImportRequiresTheReviewColumns()
        {
            string path = Path.Combine(_directory, "bad.csv");
            File.WriteAllText(path, "id,layer\r\nW-1+2,A-WALL\r\n");

            Assert.Throws<InvalidDataException>(() => new WallReviewCsv().Import(path, Candidates(), new WallReviewService(null)));
        }
    }
}
