using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProjectState
    {
        public const string CurrentSchemaVersion = "1.0";

        [JsonProperty("schemaVersion", Order = 1)]
        public string SchemaVersion { get; set; }

        [JsonProperty("project", Order = 2)]
        public ProjectInfo Project { get; set; }

        [JsonProperty("cadSource", Order = 3)]
        public CadSourceInfo CadSource { get; set; }

        [JsonProperty("revitLink", Order = 4)]
        public RevitLinkInfo RevitLink { get; set; }

        [JsonProperty("cadAudit", Order = 5)]
        public CadAuditInfo CadAudit { get; set; }
    }
}
