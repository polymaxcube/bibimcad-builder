using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProjectState
    {
        public const string CurrentSchemaVersion = "1.1";

        public static readonly string[] SupportedSchemaVersions = new[] { "1.0", CurrentSchemaVersion };

        public ProjectState()
        {
            WallCandidates = new List<WallCandidate>();
        }

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

        [JsonProperty("wallClassification", Order = 6)]
        public WallClassificationInfo WallClassification { get; set; }

        [JsonProperty("wallCandidates", Order = 7)]
        public List<WallCandidate> WallCandidates { get; set; }

        [JsonProperty("validation", Order = 8)]
        public ValidationInfo Validation { get; set; }

        public static bool IsSupportedSchemaVersion(string version)
        {
            return Array.IndexOf(SupportedSchemaVersions, version) >= 0;
        }

        // Older files lack the 1.1 sections. Fill empty lists so callers never see null collections.
        public void UpgradeToCurrentSchema()
        {
            if (!IsSupportedSchemaVersion(SchemaVersion))
                throw new InvalidOperationException("Unsupported project state schema version: " + SchemaVersion);

            SchemaVersion = CurrentSchemaVersion;

            if (CadSource != null && CadSource.WallLayers == null)
                CadSource.WallLayers = new List<string>();

            if (CadAudit == null)
                CadAudit = new CadAuditInfo();
            if (CadAudit.EntityCounts == null)
                CadAudit.EntityCounts = new Dictionary<string, int>();
            if (CadAudit.Warnings == null)
                CadAudit.Warnings = new List<string>();
            if (CadAudit.Layers == null)
                CadAudit.Layers = new List<CadLayerInfo>();

            if (WallCandidates == null)
                WallCandidates = new List<WallCandidate>();
        }
    }
}
