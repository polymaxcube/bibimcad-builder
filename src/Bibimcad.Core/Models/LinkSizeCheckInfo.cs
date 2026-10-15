using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    // Compares the CAD link as Revit draws it with the DXF. All lengths are millimeters.
    [JsonObject(MemberSerialization.OptIn)]
    public class LinkSizeCheckInfo
    {
        public LinkSizeCheckInfo()
        {
            Layers = new List<string>();
        }

        [JsonProperty("checkedAt", Order = 1)]
        public string CheckedAt { get; set; }

        [JsonProperty("result", Order = 2)]
        public string Result { get; set; }

        [JsonProperty("message", Order = 3)]
        public string Message { get; set; }

        [JsonProperty("layers", Order = 4)]
        public List<string> Layers { get; set; }

        [JsonProperty("dxfWidthMm", Order = 5)]
        public double? DxfWidthMm { get; set; }

        [JsonProperty("dxfHeightMm", Order = 6)]
        public double? DxfHeightMm { get; set; }

        [JsonProperty("linkWidthMm", Order = 7)]
        public double? LinkWidthMm { get; set; }

        [JsonProperty("linkHeightMm", Order = 8)]
        public double? LinkHeightMm { get; set; }

        [JsonProperty("ratio", Order = 9)]
        public double? Ratio { get; set; }

        [JsonProperty("likelyImportUnit", Order = 10)]
        public string LikelyImportUnit { get; set; }

        [JsonProperty("offsetXMm", Order = 11)]
        public double? OffsetXMm { get; set; }

        [JsonProperty("offsetYMm", Order = 12)]
        public double? OffsetYMm { get; set; }
    }

    public static class LinkSizeCheckResult
    {
        public const string Passed = "Passed";
        public const string ScaleMismatch = "ScaleMismatch";
        public const string Offset = "Offset";
        public const string Inconclusive = "Inconclusive";
        public const string Skipped = "Skipped";

        public static bool IsProblem(LinkSizeCheckInfo check)
        {
            return check != null
                && (check.Result == ScaleMismatch || check.Result == Offset);
        }
    }
}
