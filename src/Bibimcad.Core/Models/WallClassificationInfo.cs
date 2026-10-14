using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WallClassificationInfo
    {
        public WallClassificationInfo()
        {
            WallLayers = new List<string>();
            Warnings = new List<string>();
        }

        [JsonProperty("classifiedAt", Order = 1)]
        public string ClassifiedAt { get; set; }

        [JsonProperty("wallLayers", Order = 2)]
        public List<string> WallLayers { get; set; }

        [JsonProperty("segmentCount", Order = 3)]
        public int SegmentCount { get; set; }

        [JsonProperty("pairedSegmentCount", Order = 4)]
        public int PairedSegmentCount { get; set; }

        [JsonProperty("unpairedSegmentCount", Order = 5)]
        public int UnpairedSegmentCount { get; set; }

        [JsonProperty("candidateCount", Order = 6)]
        public int CandidateCount { get; set; }

        [JsonProperty("warnings", Order = 7)]
        public List<string> Warnings { get; set; }
    }
}
