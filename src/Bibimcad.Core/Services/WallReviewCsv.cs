using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    // Reads and writes wall_review.csv with the same columns as the Python classify_walls.py tool,
    // so the review can be done in Excel. Coordinates and lengths are millimeters.
    public class WallReviewCsv
    {
        public static readonly string[] Columns = new[]
        {
            "id", "layer", "x1", "y1", "x2", "y2", "length_cad", "orientation",
            "possible_parallel_edge", "possible_gap_cad", "wall_candidate", "approve", "wall_height_mm", "notes"
        };

        private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

        public void Export(IEnumerable<WallCandidate> candidates, string path)
        {
            if (candidates == null)
                throw new ArgumentNullException("candidates");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CSV path is required.", "path");

            StringBuilder text = new StringBuilder();
            text.Append(string.Join(",", Columns)).Append("\r\n");
            foreach (WallCandidate candidate in candidates)
            {
                string[] values = new[]
                {
                    candidate.Id,
                    candidate.Layer,
                    Number(candidate.StartXMm),
                    Number(candidate.StartYMm),
                    Number(candidate.EndXMm),
                    Number(candidate.EndYMm),
                    Number(candidate.LengthMm),
                    candidate.Orientation,
                    candidate.SourceSegmentIds == null ? string.Empty : string.Join(" + ", candidate.SourceSegmentIds.ToArray()),
                    Number(candidate.ThicknessMm),
                    (candidate.Status ?? WallCandidateStatus.Unverified).ToUpperInvariant(),
                    ApproveValue(candidate.Status),
                    candidate.HeightMm.HasValue ? Number(candidate.HeightMm.Value) : string.Empty,
                    candidate.Notes
                };

                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        text.Append(',');
                    text.Append(Quote(values[i]));
                }

                text.Append("\r\n");
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text.ToString(), Utf8WithBom);
        }

        // Applies approve, wall_height_mm, and notes from the CSV to the candidates with matching ids.
        // Other columns are informational and are ignored on import.
        public WallReviewCsvImport Import(string path, IList<WallCandidate> candidates, WallReviewService review)
        {
            if (candidates == null)
                throw new ArgumentNullException("candidates");
            if (review == null)
                throw new ArgumentNullException("review");
            if (!File.Exists(path))
                throw new FileNotFoundException("CSV file was not found: " + path, path);

            string text = File.ReadAllText(path, Encoding.UTF8);
            List<List<string>> rows = Parse(text, DetectDelimiter(text));
            if (rows.Count == 0)
                throw new InvalidDataException("CSV file is empty: " + path);

            Dictionary<string, int> header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++)
            {
                string name = rows[0][i].Trim();
                if (!header.ContainsKey(name))
                    header[name] = i;
            }

            foreach (string required in new[] { "id", "approve", "wall_height_mm", "notes" })
            {
                if (!header.ContainsKey(required))
                    throw new InvalidDataException("CSV column '" + required + "' is missing: " + path);
            }

            Dictionary<string, WallCandidate> byId = new Dictionary<string, WallCandidate>(StringComparer.Ordinal);
            foreach (WallCandidate candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.Id))
                    byId[candidate.Id] = candidate;
            }

            WallReviewCsvImport result = new WallReviewCsvImport();
            for (int r = 1; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                string id = Cell(row, header["id"]).Trim();
                if (id.Length == 0)
                    continue;

                WallCandidate candidate;
                if (!byId.TryGetValue(id, out candidate))
                {
                    result.UnknownIds.Add(id);
                    continue;
                }

                string rowLabel = "Row " + (r + 1).ToString(CultureInfo.InvariantCulture) + " (" + id + "): ";
                string heightText = Cell(row, header["wall_height_mm"]).Trim();
                double? height = null;
                if (heightText.Length > 0)
                {
                    double parsed;
                    if (!TryParseNumber(heightText, out parsed) || !WallReviewService.IsValidHeight(parsed))
                    {
                        result.Errors.Add(rowLabel + "wall_height_mm '" + heightText + "' is not a positive number.");
                        continue;
                    }

                    height = parsed;
                }

                string approve = Cell(row, header["approve"]).Trim().ToUpperInvariant();
                string status;
                if (approve == "YES" || approve == "Y" || approve == "APPROVED")
                    status = WallCandidateStatus.Approved;
                else if (approve == "REJECT" || approve == "REJECTED")
                    status = WallCandidateStatus.Rejected;
                else if (approve == "NO" || approve == "N" || approve.Length == 0)
                    status = WallCandidateStatus.Unverified;
                else
                {
                    result.Errors.Add(rowLabel + "approve must be YES, NO, or REJECT, not '" + approve + "'.");
                    continue;
                }

                candidate.HeightMm = height;
                candidate.Notes = Cell(row, header["notes"]);
                if (status == WallCandidateStatus.Approved)
                {
                    if (!review.TryApprove(candidate))
                    {
                        candidate.Status = WallCandidateStatus.Unverified;
                        result.ApprovalsWithoutHeight.Add(id);
                    }
                }
                else
                {
                    candidate.Status = status;
                }

                result.UpdatedCount++;
            }

            return result;
        }

        private static string ApproveValue(string status)
        {
            if (string.Equals(status, WallCandidateStatus.Approved, StringComparison.Ordinal))
                return "YES";
            if (string.Equals(status, WallCandidateStatus.Rejected, StringComparison.Ordinal))
                return "REJECT";
            return "NO";
        }

        private static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool TryParseNumber(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.IndexOfAny(new[] { ',', ';', '"', '\r', '\n' }) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Cell(List<string> row, int index)
        {
            return index < row.Count ? row[index] : string.Empty;
        }

        // Excel saves with ';' in regions that use ',' as the decimal separator.
        private static char DetectDelimiter(string text)
        {
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            string header = end < 0 ? text : text.Substring(0, end);
            return header.IndexOf(',') < 0 && header.IndexOf(';') >= 0 ? ';' : ',';
        }

        private static List<List<string>> Parse(string text, char delimiter)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            int i = 0;
            if (text.Length > 0 && text[0] == '\uFEFF')
                i = 1;

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(field.ToString());
                    field.Clear();
                    AddRow(rows, row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                AddRow(rows, row);
            }

            return rows;
        }

        private static void AddRow(List<List<string>> rows, List<string> row)
        {
            if (row.Count == 1 && row[0].Length == 0)
                return;
            rows.Add(row);
        }
    }

    public class WallReviewCsvImport
    {
        public WallReviewCsvImport()
        {
            UnknownIds = new List<string>();
            ApprovalsWithoutHeight = new List<string>();
            Errors = new List<string>();
        }

        public int UpdatedCount { get; set; }

        public List<string> UnknownIds { get; private set; }

        public List<string> ApprovalsWithoutHeight { get; private set; }

        public List<string> Errors { get; private set; }
    }
}
