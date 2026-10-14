using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CadSourceInfo
    {
        public CadSourceInfo()
        {
            WallLayers = new List<string>();
        }

        [JsonProperty("dxfPath", Order = 1)]
        public string DxfPath { get; set; }

        [JsonProperty("dwgName", Order = 2)]
        public string DwgName { get; set; }

        [JsonProperty("headerInsUnits", Order = 3)]
        public string HeaderInsUnits { get; set; }

        [JsonProperty("approvedCoordinateUnit", Order = 4)]
        public string ApprovedCoordinateUnit { get; set; }

        [JsonProperty("mmPerCadUnit", Order = 5)]
        public double MmPerCadUnit { get; set; }

        [JsonProperty("unitCalibrationApproved", Order = 6)]
        public bool UnitCalibrationApproved { get; set; }

        [JsonProperty("calibrationNote", Order = 7)]
        public string CalibrationNote { get; set; }

        [JsonProperty("wallLayers", Order = 8)]
        public List<string> WallLayers { get; set; }
    }
}
