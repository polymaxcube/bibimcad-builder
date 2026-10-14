using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bibimcad.Models;
using Bibimcad.Services;
using Bibimcad.UI;
using Bibimcad.Utilities;
using netDxf;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdReadDxf : IExternalCommand
    {
        private const int MaxLayersShown = 15;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                JsonProjectRepository repository = new JsonProjectRepository(PathUtils.GetProjectStatePath());
                ProjectState state = repository.Load();
                state.UpgradeToCurrentSchema();
                if (state.CadSource == null || string.IsNullOrWhiteSpace(state.CadSource.DxfPath))
                {
                    BibimcadDialog.Show("Project state is incomplete.", "cadSource.dxfPath is missing from project_state.json.");
                    message = "DXF path is missing.";
                    return Result.Failed;
                }

                string dxfPath = state.CadSource.DxfPath.Trim();
                DxfDocument document = DxfLoader.Load(dxfPath);
                state.CadAudit = new DxfAuditService().Audit(document, dxfPath, state.CadSource, DateTime.Now);
                repository.Save(state);

                LoggingUtils.Info(
                    "Read DXF "
                    + dxfPath
                    + ": "
                    + state.CadAudit.LayerCount.ToString(CultureInfo.InvariantCulture)
                    + " layers, "
                    + state.CadAudit.EntityCounts.Values.Sum().ToString(CultureInfo.InvariantCulture)
                    + " entities, "
                    + state.CadAudit.Warnings.Count.ToString(CultureInfo.InvariantCulture)
                    + " warnings.");

                BibimcadDialog.Show("DXF read", BuildReport(state));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return BibimcadDialog.Fail(ref message, "Read DXF failed.", ex);
            }
        }

        private static string BuildReport(ProjectState state)
        {
            CadAuditInfo audit = state.CadAudit;
            List<string> lines = new List<string>();
            lines.Add("File: " + audit.SourceFile);
            lines.Add("DXF version: " + audit.DxfVersion);
            lines.Add("Header units: " + state.CadSource.HeaderInsUnits);
            lines.Add("Layers: " + audit.LayerCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Blocks: " + audit.BlockCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);

            lines.Add("Model space entities:");
            foreach (KeyValuePair<string, int> pair in audit.EntityCounts.OrderByDescending(p => p.Value))
                lines.Add("  " + pair.Key + ": " + pair.Value.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);

            List<CadLayerInfo> busiest = audit.Layers
                .Where(l => l.EntityCount > 0)
                .OrderByDescending(l => l.EntityCount)
                .Take(MaxLayersShown)
                .ToList();
            lines.Add("Busiest layers (all layers are saved in project_state.json):");
            foreach (CadLayerInfo layer in busiest)
                lines.Add("  " + layer.Name + ": " + layer.EntityCount.ToString(CultureInfo.InvariantCulture));

            if (audit.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Warnings:");
                foreach (string warning in audit.Warnings)
                    lines.Add("  " + warning);
            }

            lines.Add(string.Empty);
            lines.Add("Next: list the wall layers in cadSource.wallLayers, then run 04 Classify.");
            lines.Add("Saved: " + PathUtils.GetProjectStatePath());
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
