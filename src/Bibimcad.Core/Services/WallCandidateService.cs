using System;
using System.Collections.Generic;
using System.Globalization;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    // Pairs parallel wall-layer segments into wall candidates. A pair is a geometry hint only:
    // every candidate starts Unverified and must be approved by a person before walls are created.
    public class WallCandidateService
    {
        private const int CoordinateDecimals = 3;

        public WallClassificationResult Classify(IList<CadSegment> segments, WallClassificationOptions options)
        {
            if (segments == null)
                throw new ArgumentNullException("segments");
            if (options == null)
                throw new ArgumentNullException("options");

            List<Prepared> prepared = new List<Prepared>();
            foreach (CadSegment segment in segments)
            {
                Prepared item = Prepared.From(segment);
                if (item != null)
                    prepared.Add(item);
            }

            prepared.Sort((a, b) => a.AngleDegrees.CompareTo(b.AngleDegrees));

            List<PairMatch> matches = new List<PairMatch>();
            double tolerance = options.ParallelToleranceDegrees;
            for (int i = 0; i < prepared.Count; i++)
            {
                for (int j = i + 1; j < prepared.Count && prepared[j].AngleDegrees - prepared[i].AngleDegrees <= tolerance; j++)
                    TryMatch(prepared[i], prepared[j], options, matches);

                // Lines at 179.9 and 0.1 degrees are parallel; compare across the wrap.
                if (prepared[i].AngleDegrees > 180.0 - tolerance)
                {
                    for (int j = 0; j < i && prepared[j].AngleDegrees + 180.0 - prepared[i].AngleDegrees <= tolerance; j++)
                        TryMatch(prepared[i], prepared[j], options, matches);
                }
            }

            matches.Sort(ComparePairs);

            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            WallClassificationResult result = new WallClassificationResult();
            foreach (PairMatch match in matches)
            {
                if (used.Contains(match.A.Segment.Id) || used.Contains(match.B.Segment.Id))
                    continue;

                used.Add(match.A.Segment.Id);
                used.Add(match.B.Segment.Id);
                result.Candidates.Add(BuildCandidate(match, options));
            }

            result.Candidates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            result.SegmentCount = prepared.Count;
            result.PairedSegmentCount = used.Count;
            result.UnpairedSegmentCount = prepared.Count - used.Count;

            if (result.UnpairedSegmentCount > 0)
            {
                result.Warnings.Add(
                    result.UnpairedSegmentCount.ToString(CultureInfo.InvariantCulture)
                    + " wall-layer segments have no parallel partner "
                    + Format(options.MinGapMm)
                    + " to "
                    + Format(options.MaxGapMm)
                    + " mm away with at least "
                    + Format(options.MinOverlapRatio * 100.0)
                    + "% overlap. They are not candidates.");
            }

            return result;
        }

        private static void TryMatch(Prepared a, Prepared b, WallClassificationOptions options, List<PairMatch> matches)
        {
            if (string.Equals(a.Segment.Id, b.Segment.Id, StringComparison.Ordinal))
                return;

            // Measure B in A's frame: t along A from its start, d across A to the left.
            double tB1 = Dot(b.X1 - a.X1, b.Y1 - a.Y1, a.Ux, a.Uy);
            double tB2 = Dot(b.X2 - a.X1, b.Y2 - a.Y1, a.Ux, a.Uy);
            double dB1 = Dot(b.X1 - a.X1, b.Y1 - a.Y1, -a.Uy, a.Ux);
            double dB2 = Dot(b.X2 - a.X1, b.Y2 - a.Y1, -a.Uy, a.Ux);

            double offset = (dB1 + dB2) / 2.0;
            double gap = Math.Abs(offset);
            if (gap < options.MinGapMm || gap > options.MaxGapMm)
                return;

            double low = Math.Max(0.0, Math.Min(tB1, tB2));
            double high = Math.Min(a.Length, Math.Max(tB1, tB2));
            double overlap = high - low;
            double shortest = Math.Min(a.Length, b.Length);
            if (overlap <= 0.0 || shortest <= 0.0)
                return;

            double ratio = overlap / shortest;
            if (ratio < options.MinOverlapRatio)
                return;

            PairMatch match = new PairMatch();
            match.A = a;
            match.B = b;
            match.Gap = gap;
            match.Ratio = ratio;
            match.Offset = offset;
            match.Low = low;
            match.High = high;
            matches.Add(match);
        }

        private static int ComparePairs(PairMatch x, PairMatch y)
        {
            int result = (1.0 - x.Ratio).CompareTo(1.0 - y.Ratio);
            if (result != 0)
                return result;

            result = x.Gap.CompareTo(y.Gap);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(x.Key, y.Key);
            return result;
        }

        private static WallCandidate BuildCandidate(PairMatch match, WallClassificationOptions options)
        {
            Prepared a = match.A;
            double half = match.Offset / 2.0;
            double nx = -a.Uy;
            double ny = a.Ux;

            double x1 = a.X1 + a.Ux * match.Low + nx * half;
            double y1 = a.Y1 + a.Uy * match.Low + ny * half;
            double x2 = a.X1 + a.Ux * match.High + nx * half;
            double y2 = a.Y1 + a.Uy * match.High + ny * half;

            // Store the centerline in a fixed direction so the same wall always serializes the same way.
            if (x2 < x1 || (x2 == x1 && y2 < y1))
            {
                double tx = x1;
                double ty = y1;
                x1 = x2;
                y1 = y2;
                x2 = tx;
                y2 = ty;
            }

            string[] ids = new[] { match.A.Segment.Id, match.B.Segment.Id };
            Array.Sort(ids, StringComparer.Ordinal);

            WallCandidate candidate = new WallCandidate();
            candidate.Id = "W-" + ids[0] + "+" + ids[1];
            candidate.Layer = string.Equals(match.A.Segment.Layer, match.B.Segment.Layer, StringComparison.OrdinalIgnoreCase)
                ? match.A.Segment.Layer
                : match.A.Segment.Layer + " / " + match.B.Segment.Layer;
            candidate.SourceSegmentIds.AddRange(ids);
            candidate.StartXMm = Round(x1);
            candidate.StartYMm = Round(y1);
            candidate.EndXMm = Round(x2);
            candidate.EndYMm = Round(y2);
            candidate.LengthMm = Round(match.High - match.Low);
            candidate.ThicknessMm = Round(match.Gap);
            candidate.Orientation = Orientation(a.AngleDegrees, options.OrientationToleranceDegrees);
            candidate.Status = WallCandidateStatus.Unverified;
            return candidate;
        }

        public static string Orientation(double angleDegrees, double toleranceDegrees)
        {
            double fromHorizontal = Math.Min(angleDegrees, 180.0 - angleDegrees);
            if (fromHorizontal <= toleranceDegrees)
                return "horizontal";
            if (Math.Abs(angleDegrees - 90.0) <= toleranceDegrees)
                return "vertical";
            return "angled";
        }

        private static double Dot(double x, double y, double ux, double uy)
        {
            return x * ux + y * uy;
        }

        private static double Round(double value)
        {
            return Math.Round(value, CoordinateDecimals, MidpointRounding.AwayFromZero);
        }

        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private sealed class Prepared
        {
            public CadSegment Segment;
            public double X1;
            public double Y1;
            public double X2;
            public double Y2;
            public double Ux;
            public double Uy;
            public double Length;
            public double AngleDegrees;

            public static Prepared From(CadSegment segment)
            {
                double dx = segment.X2 - segment.X1;
                double dy = segment.Y2 - segment.Y1;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= 0.0)
                    return null;

                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (angle < 0.0)
                    angle += 180.0;
                if (angle >= 180.0)
                    angle -= 180.0;

                Prepared item = new Prepared();
                item.Segment = segment;
                item.X1 = segment.X1;
                item.Y1 = segment.Y1;
                item.X2 = segment.X2;
                item.Y2 = segment.Y2;
                item.Ux = dx / length;
                item.Uy = dy / length;
                item.Length = length;
                item.AngleDegrees = angle;
                return item;
            }
        }

        private sealed class PairMatch
        {
            public Prepared A;
            public Prepared B;
            public double Gap;
            public double Ratio;
            public double Offset;
            public double Low;
            public double High;

            public string Key
            {
                get
                {
                    return string.CompareOrdinal(A.Segment.Id, B.Segment.Id) < 0
                        ? A.Segment.Id + "+" + B.Segment.Id
                        : B.Segment.Id + "+" + A.Segment.Id;
                }
            }
        }
    }

    public class WallClassificationOptions
    {
        public WallClassificationOptions()
        {
            MinGapMm = 75.0;
            MaxGapMm = 350.0;
            MinOverlapRatio = 0.70;
            ParallelToleranceDegrees = 1.0;
            OrientationToleranceDegrees = 1.0;
        }

        public double MinGapMm { get; set; }

        public double MaxGapMm { get; set; }

        public double MinOverlapRatio { get; set; }

        public double ParallelToleranceDegrees { get; set; }

        public double OrientationToleranceDegrees { get; set; }
    }

    public class WallClassificationResult
    {
        public WallClassificationResult()
        {
            Candidates = new List<WallCandidate>();
            Warnings = new List<string>();
        }

        public List<WallCandidate> Candidates { get; private set; }

        public int SegmentCount { get; set; }

        public int PairedSegmentCount { get; set; }

        public int UnpairedSegmentCount { get; set; }

        public List<string> Warnings { get; private set; }
    }
}
