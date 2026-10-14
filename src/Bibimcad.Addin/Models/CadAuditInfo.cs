using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CadAuditInfo
    {
        public CadAuditInfo()
        {
            EntityCounts = new Dictionary<string, int>();
            Warnings = new List<string>();
        }

        [JsonProperty("layerCount", Order = 1)]
        public int LayerCount { get; set; }

        [JsonProperty("blockCount", Order = 2)]
        public int BlockCount { get; set; }

        [JsonProperty("entityCounts", Order = 3)]
        public Dictionary<string, int> EntityCounts { get; set; }

        [JsonProperty("warnings", Order = 4)]
        public List<string> Warnings { get; set; }
    }
}
