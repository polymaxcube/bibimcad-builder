using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    public static class HelpSeverity
    {
        public const string Problem = "Problem";
        public const string Warning = "Warning";
        public const string Ok = "OK";
        public const string Guide = "Guide";
    }

    public class HelpTopic
    {
        public HelpTopic(string severity, string title, string detail, params string[] steps)
        {
            Severity = severity;
            Title = title;
            Detail = detail;
            Steps = new List<string>(steps ?? new string[0]);
        }

        public string Severity { get; private set; }

        public string Title { get; private set; }

        public string Detail { get; private set; }

        public List<string> Steps { get; private set; }
    }

    // Turns project_state.json into a list of problems with step-by-step fixes. Reads only; never changes the state.
    public static class HelpAdvisor
    {
        private const string RelinkTitle = "Relink K1.dwg in millimeters";

        public static List<HelpTopic> MissingStateFile(string path)
        {
            return new List<HelpTopic>
            {
                new HelpTopic(
                    HelpSeverity.Problem,
                    "project_state.json is missing",
                    "Bibimcad keeps all project data in " + path + ". The file does not exist, so no button can work.",
                    "Close Revit.",
                    "Copy data\\project_state.json from the Bibimcad source folder to C:\\Bibimcad\\data\\.",
                    "Start Revit and click 01 Inspect."),
            };
        }

        public static List<HelpTopic> UnreadableStateFile(string path, string error)
        {
            return new List<HelpTopic>
            {
                new HelpTopic(
                    HelpSeverity.Problem,
                    "project_state.json cannot be read",
                    path + " could not be read: " + error,
                    "Open the file in Notepad (button below).",
                    "Look for a missing comma, quote, or bracket near the place the error mentions. Most often it is the line you edited last, such as wallLayers.",
                    "Example of a correct line: \"wallLayers\": [\"HV-WALL\"],",
                    "Save the file and click 08 Help again."),
            };
        }

        public static List<HelpTopic> Diagnose(ProjectState state, Func<string, bool> fileExists)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (fileExists == null)
                throw new ArgumentNullException("fileExists");

            List<HelpTopic> topics = new List<HelpTopic>();
            CadSourceInfo cad = state.CadSource;
            CadAuditInfo audit = state.CadAudit;
            List<WallCandidate> candidates = state.WallCandidates ?? new List<WallCandidate>();
            List<string> wallLayers = cad == null || cad.WallLayers == null
                ? new List<string>()
                : cad.WallLayers.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            AddLinkTopics(topics, state);
            AddDxfTopics(topics, cad, audit, fileExists);
            AddLayerTopics(topics, cad, audit, wallLayers);
            AddWallTopics(topics, state, candidates, wallLayers);

            if (topics.Count == 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Ok,
                    "No problems found",
                    "Everything Bibimcad can check from project_state.json looks right."));
            }

            return topics;
        }

        public static List<HelpTopic> CommonProblems()
        {
            return new List<HelpTopic>
            {
                new HelpTopic(
                    HelpSeverity.Guide,
                    "Walls appear far from the CAD drawing, or the wrong size",
                    "Bibimcad places walls with the DXF coordinates in millimeters. If K1.dwg was linked with Import units = Auto-Detect, "
                        + "Revit may read its Inches header and draw the CAD 25.4 times too big. The walls are then correct, but sit near the origin, away from the CAD. "
                        + "Click 02 Coordinates: its size check shows the ratio.",
                    RelinkSteps()),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "03 Read DXF shows only INSERT: 1 (the drawing is one block)",
                    "All lines are inside one block, so Bibimcad sees no wall lines.",
                    "Open a copy of K1.dwg in AutoCAD.",
                    "Type EXPLODE (or BURST), click the drawing, press Enter. Repeat until clicking a wall selects a single line.",
                    "Save As > AutoCAD 2018 DXF to the path in cadSource.dxfPath (default C:\\Bibimcad\\input\\K1.dxf).",
                    "Do not explode the K1.dwg that is linked in Revit.",
                    "Click 03 Read DXF again."),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "Which layer holds the walls?",
                    "Layer names can be anything (HV-WALL, 0, Thai or Korean names). Bibimcad only needs the exact names.",
                    "In Revit, click the K1.dwg drawing, then Modify | K1.dwg > Query, and click a wall line. Revit shows its layer.",
                    "Repeat on a few different walls; walls can be on more than one layer.",
                    "Write the names in project_state.json, for example \"wallLayers\": [\"HV-WALL\"],",
                    "Click 04 Classify."),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "Walls were snapped or moved by hand",
                    "07 Validate compares walls with where Bibimcad placed them, so moved walls fail. Fix the cause instead (usually the link units), then rebuild.",
                    "Delete the Bibimcad walls (types named \"Bibimcad - ... mm\").",
                    "Fix the link (see the first topic) and click 02 Coordinates.",
                    "Click 06 Generate, then 07 Validate."),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "The drawing changed",
                    "Re-running keeps review decisions for walls whose geometry did not change.",
                    "Save the new DXF over the old one.",
                    "Click 03 Read DXF, then 04 Classify.",
                    "Click 05 Review to check new candidates (Status filter: Unverified).",
                    "Click 06 Generate and 07 Validate."),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "Undo or rebuild generated walls",
                    "06 Generate never duplicates walls. It skips walls that still exist and recreates deleted ones.",
                    "Delete the walls you want rebuilt in Revit.",
                    "Change heights in 05 Review if needed, then Save.",
                    "Click 06 Generate."),
                new HelpTopic(
                    HelpSeverity.Guide,
                    "Bibimcad cannot read DWG",
                    "Bibimcad reads DXF files (AutoCAD 2000 or later). The DWG stays linked in Revit for position.",
                    "In AutoCAD: Save As > AutoCAD 2018 DXF.",
                    "Put the path in cadSource.dxfPath."),
            };
        }

        private static void AddLinkTopics(List<HelpTopic> topics, ProjectState state)
        {
            RevitLinkInfo link = state.RevitLink;
            if (link == null || link.Transform == null)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "The CAD link has not been checked",
                    "02 Coordinates has not saved the position of K1.dwg yet.",
                    "Open the Revit project that has K1.dwg linked.",
                    "Click 02 Coordinates."));
                return;
            }

            LinkSizeCheckInfo check = link.SizeCheck;
            if (check == null)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "The link size has not been checked",
                    "This project was checked before the link size check existed.",
                    "Click 02 Coordinates again. It now also compares K1.dwg in Revit with the DXF."));
                return;
            }

            if (check.Result == LinkSizeCheckResult.ScaleMismatch)
            {
                bool bigger = check.Ratio.HasValue && check.Ratio.Value > 1.0;
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    bigger ? RelinkTitle : "The link is drawn smaller than the DXF",
                    check.Message + SizeLine(check),
                    bigger
                        ? RelinkSteps()
                        : new[]
                        {
                            "Use Measure in Revit on a CAD wall whose real length you know.",
                            "If Revit shows the wrong length, relink K1.dwg with Import units = Millimeter (Insert > Link CAD).",
                            "If Revit shows the right length, cadSource.mmPerCadUnit in project_state.json is wrong. Ask the person who approved the calibration.",
                            "Click 02 Coordinates again.",
                        }));
            }
            else if (check.Result == LinkSizeCheckResult.Offset)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "The DXF is shifted from the linked drawing",
                    check.Message,
                    "Save the DXF again from the exact K1.dwg that is linked in Revit. Use only EXPLODE or BURST; do not MOVE, WBLOCK, or change the base point.",
                    "Click 03 Read DXF and 02 Coordinates again.",
                    "Delete wrongly placed Bibimcad walls and click 06 Generate."));
            }
            else if (check.Result == LinkSizeCheckResult.Inconclusive || check.Result == LinkSizeCheckResult.Skipped)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "The link size could not be checked",
                    check.Message,
                    "Make sure cadSource.dxfPath points at the DXF of the linked K1.dwg and that cadSource.wallLayers is filled in.",
                    "Click 02 Coordinates again.",
                    "If it still cannot check, use Measure in Revit on a CAD wall of known length."));
            }
        }

        private static void AddDxfTopics(List<HelpTopic> topics, CadSourceInfo cad, CadAuditInfo audit, Func<string, bool> fileExists)
        {
            if (cad == null)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "cadSource is missing from project_state.json",
                    "Bibimcad does not know which drawing to read.",
                    "Copy the cadSource section from the template data\\project_state.json and fill in dxfPath and dwgName."));
                return;
            }

            if (string.IsNullOrWhiteSpace(cad.DxfPath))
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "No DXF path",
                    "cadSource.dxfPath is empty.",
                    "Save K1.dwg as DXF (AutoCAD 2018 DXF).",
                    "Write its path in cadSource.dxfPath, for example \"C:\\\\Bibimcad\\\\input\\\\K1.dxf\" (double backslashes in JSON)."));
            }
            else if (string.Equals(Path.GetExtension(cad.DxfPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "dxfPath points at a DWG file",
                    "Bibimcad reads DXF, not DWG: " + cad.DxfPath,
                    "In AutoCAD: Save As > AutoCAD 2018 DXF.",
                    "Change cadSource.dxfPath to the .dxf file."));
            }
            else if (!fileExists(cad.DxfPath))
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "The DXF file is missing",
                    "No file at " + cad.DxfPath + ".",
                    "Save K1.dwg as DXF (AutoCAD 2018 DXF) to exactly that path, or change cadSource.dxfPath.",
                    "Click 03 Read DXF."));
            }

            if (!cad.UnitCalibrationApproved || cad.MmPerCadUnit <= 0.0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "The unit calibration is not approved",
                    "Classification needs cadSource.unitCalibrationApproved = true and a positive mmPerCadUnit.",
                    "In AutoCAD, measure a known dimension with DIST (for example 6000 mm).",
                    "If DIST shows the same number in drawing units, mmPerCadUnit is 1.",
                    "Set unitCalibrationApproved to true and describe the check in calibrationNote."));
            }

            if (audit == null || string.IsNullOrWhiteSpace(audit.ReadAt))
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "The DXF has not been read yet",
                    "03 Read DXF lists the layers you need for wallLayers.",
                    "Click 03 Read DXF."));
                return;
            }

            int insertCount = Count(audit, "INSERT");
            int lineCount = Count(audit, "LINE") + Count(audit, "LWPOLYLINE") + Count(audit, "POLYLINE");
            if (insertCount > 0 && lineCount == 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "The drawing is inside a block",
                    "03 Read DXF found " + insertCount.ToString(CultureInfo.InvariantCulture)
                        + " block reference(s) and no loose lines. Bibimcad only reads loose lines.",
                    "Open a copy of K1.dwg in AutoCAD.",
                    "EXPLODE (or BURST) the drawing until a wall line selects on its own.",
                    "Save As > AutoCAD 2018 DXF over " + cad.DxfPath + ".",
                    "Click 03 Read DXF again."));
            }
        }

        private static void AddLayerTopics(List<HelpTopic> topics, CadSourceInfo cad, CadAuditInfo audit, List<string> wallLayers)
        {
            if (cad == null)
                return;

            List<CadLayerInfo> layers = audit == null || audit.Layers == null ? new List<CadLayerInfo>() : audit.Layers;
            if (wallLayers.Count == 0)
            {
                string busiest = string.Join(
                    ", ",
                    layers.Where(l => l.EntityCount > 0)
                        .OrderByDescending(l => l.EntityCount)
                        .Take(5)
                        .Select(l => l.Name + " (" + l.EntityCount.ToString(CultureInfo.InvariantCulture) + ")")
                        .ToArray());

                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "No wall layers chosen",
                    "cadSource.wallLayers is empty, so 04 Classify cannot run."
                        + (busiest.Length > 0 ? " Busiest layers in the DXF: " + busiest + "." : string.Empty),
                    "Find the wall layer: click K1.dwg in Revit, Modify | K1.dwg > Query, click a wall line.",
                    "Open project_state.json (button below) and set, for example, \"wallLayers\": [\"HV-WALL\"],",
                    "Save the file and click 04 Classify."));
                return;
            }

            if (layers.Count == 0)
                return;

            HashSet<string> known = new HashSet<string>(layers.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            List<string> unknown = wallLayers.Where(l => !known.Contains(l.Trim())).ToList();
            if (unknown.Count > 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "Wall layer name not found in the DXF",
                    "These names in cadSource.wallLayers are not layers of the DXF: " + string.Join(", ", unknown.ToArray()) + ".",
                    "Compare the spelling with the layer list from 03 Read DXF, including capitals, dashes, and spaces.",
                    "Fix the names in project_state.json and click 04 Classify."));
            }
        }

        private static void AddWallTopics(List<HelpTopic> topics, ProjectState state, List<WallCandidate> candidates, List<string> wallLayers)
        {
            WallClassificationInfo classification = state.WallClassification;
            if (classification == null)
            {
                if (wallLayers.Count > 0)
                {
                    topics.Add(new HelpTopic(
                        HelpSeverity.Warning,
                        "Walls have not been classified yet",
                        "Wall layers are set, but 04 Classify has not run.",
                        "Click 04 Classify."));
                }

                return;
            }

            if (classification.CandidateCount == 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "04 Classify found no walls",
                    "Bibimcad pairs parallel lines 75 to 350 mm apart. None were found on "
                        + string.Join(", ", classification.WallLayers.ToArray()) + ".",
                    "Check that wallLayers holds the layer of the wall lines (Query in Revit).",
                    "Check that 03 Read DXF shows LINE or LWPOLYLINE entities, not only INSERT.",
                    "Check the units: measure a wall thickness in AutoCAD with DIST. A 200 mm wall must measure 200 when mmPerCadUnit is 1."));
                return;
            }

            List<WallCandidate> approved = candidates.Where(WallCandidateStatus.IsApproved).ToList();
            if (approved.Count == 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "No walls approved yet",
                    candidates.Count.ToString(CultureInfo.InvariantCulture) + " candidates are waiting for review.",
                    "Click 05 Review, select the real walls, click Approve selected, then Save."));
                return;
            }

            int withoutHeight = approved.Count(c => !c.HeightMm.HasValue || c.HeightMm.Value <= 0.0);
            if (withoutHeight > 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "Approved walls without a height",
                    withoutHeight.ToString(CultureInfo.InvariantCulture) + " approved candidates have no height, so 06 Generate cannot build them.",
                    "Click 05 Review, filter Status = Approved, select the rows, type a height, click Set height for selected, then Save.",
                    "Or set project.defaultWallHeightMm, for example 3000."));
            }

            int notBuilt = approved.Count(c => !c.RevitElementId.HasValue);
            if (notBuilt > 0)
            {
                topics.Add(new HelpTopic(
                    HelpSeverity.Warning,
                    "Approved walls not in Revit yet",
                    notBuilt.ToString(CultureInfo.InvariantCulture) + " approved candidates have no Revit wall.",
                    "Click 06 Generate."));
            }

            ValidationInfo validation = state.Validation;
            if (validation != null && validation.FailedCount > 0)
            {
                bool linkProblem = state.RevitLink != null && LinkSizeCheckResult.IsProblem(state.RevitLink.SizeCheck);
                string firstReason = validation.Failures.Count > 0 ? validation.Failures[0].Reason : string.Empty;
                topics.Add(new HelpTopic(
                    HelpSeverity.Problem,
                    "07 Validate found " + validation.FailedCount.ToString(CultureInfo.InvariantCulture) + " wall(s) that do not match",
                    (firstReason.Length > 0 ? "First failure: " + firstReason + " " : string.Empty)
                        + (linkProblem ? "The link size check also failed; fix the link first." : "Walls may have been moved or edited by hand."),
                    linkProblem ? "Fix the link problem above." : "Do not move Bibimcad walls by hand.",
                    "Delete the failing walls and click 06 Generate.",
                    "Click 07 Validate."));
            }
        }

        private static string[] RelinkSteps()
        {
            return new[]
            {
                "Delete the Bibimcad walls (wall types named \"Bibimcad - ... mm\").",
                "Manage > Manage Links > CAD Formats: select K1.dwg, Remove, OK.",
                "Insert > Link CAD and choose K1.dwg.",
                "At the bottom of that window set Import units = Millimeter, Positioning = Auto - Origin to Origin, Place at = Level 1.",
                "Untick \"Current view only\" and \"Correct lines that are slightly off axis\", then click Open.",
                "Click the new link and press PN to pin it.",
                "Click 02 Coordinates. The size check must say Passed (ratio 1).",
                "Click 06 Generate, then 07 Validate. Approvals are kept.",
                "Grids and columns drawn on the old, oversized CAD will not match the new link. Check them.",
            };
        }

        private static string SizeLine(LinkSizeCheckInfo check)
        {
            if (!check.DxfWidthMm.HasValue || !check.LinkWidthMm.HasValue)
                return string.Empty;

            return string.Format(
                CultureInfo.InvariantCulture,
                " (DXF {0:#,0} x {1:#,0} mm, link in Revit {2:#,0} x {3:#,0} mm.)",
                check.DxfWidthMm.Value,
                check.DxfHeightMm ?? 0.0,
                check.LinkWidthMm.Value,
                check.LinkHeightMm ?? 0.0);
        }

        private static int Count(CadAuditInfo audit, string type)
        {
            int count;
            return audit.EntityCounts != null && audit.EntityCounts.TryGetValue(type, out count) ? count : 0;
        }
    }
}
