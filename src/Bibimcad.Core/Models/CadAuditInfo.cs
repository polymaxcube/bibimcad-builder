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
            Layers = new List<CadLayerInfo>();
        }

        [JsonProperty("layerCount", Order = 1)]
        public int LayerCount { get; set; }

        [JsonProperty("blockCount", Order = 2)]
        public int BlockCount { get; set; }

        [JsonProperty("entityCounts", Order = 3)]
        public Dictionary<string, int> EntityCounts { get; set; }

        [JsonProperty("warnings", Order = 4)]
        public List<string> Warnings { get; set; }

        [JsonProperty("sourceFile", Order = 5)]
        public string SourceFile { get; set; }

        [JsonProperty("dxfVersion", Order = 6)]
        public string DxfVersion { get; set; }

        [JsonProperty("readAt", Order = 7)]
        public string ReadAt { get; set; }

        [JsonProperty("layers", Order = 8)]
        public List<CadLayerInfo> Layers { get; set; }
    }
}
