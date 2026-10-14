using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Bibimcad.Models;
using Bibimcad.Utilities;

namespace Bibimcad.Services
{
    public class WallGenerationService
    {
        private readonly CoordinateTransformService _coordinates = new CoordinateTransformService();

        // The named level from project_state.json, otherwise the level of the active plan view.
        public static Level ResolveLevel(Document document, string targetLevelName, View activeView, out string problem)
        {
            problem = null;
            if (!string.IsNullOrWhiteSpace(targetLevelName))
            {
                Level named = new FilteredElementCollector(document)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => string.Equals(l.Name, targetLevelName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (named == null)
                    problem = "project.targetLevelName is '" + targetLevelName + "', but no level has that name.";
                return named;
            }

            Level viewLevel = activeView == null ? null : activeView.GenLevel;
            if (viewLevel == null)
                problem = "Set project.targetLevelName in project_state.json, or run the command from a floor plan view.";
            return viewLevel;
        }

        public static Element FindExistingWall(Document document, WallCandidate candidate)
        {
            if (!candidate.RevitElementId.HasValue)
                return null;

            Element element = document.GetElement(ToElementId(candidate.RevitElementId.Value));
            return element is Wall && element.IsValidObject ? element : null;
        }

        public static ElementId ToElementId(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
                throw new InvalidOperationException("Revit 2023 element ids are 32-bit. Stored id is out of range: " + value);

            return new ElementId((int)value);
        }

        // Creates walls for approved candidates that have no live Revit wall. Revit ids are written
        // back to the candidates only when the transaction commits.
        public WallGenerationReport Generate(
            Document document,
            IList<WallCandidate> candidates,
            Transform linkTransform,
            Level level)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (candidates == null)
                throw new ArgumentNullException("candidates");
            if (linkTransform == null)
                throw new ArgumentNullException("linkTransform");
            if (level == null)
                throw new ArgumentNullException("level");

            WallGenerationReport report = new WallGenerationReport();
            Dictionary<WallCandidate, ElementId> created = new Dictionary<WallCandidate, ElementId>();
            WallTypeCache wallTypes = new WallTypeCache(document);
            double shortCurveTolerance = document.Application.ShortCurveTolerance;

            using (Transaction transaction = new Transaction(document, "Bibimcad - Generate walls"))
            {
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                WarningSwallower swallower = new WarningSwallower();
                options.SetFailuresPreprocessor(swallower);
                transaction.SetFailureHandlingOptions(options);
                transaction.Start();

                foreach (WallCandidate candidate in candidates)
                {
                    if (!WallCandidateStatus.IsApproved(candidate))
                        continue;

                    if (FindExistingWall(document, candidate) != null)
                    {
                        report.SkippedExisting++;
                        continue;
                    }

                    try
                    {
                        ElementId wallId = CreateWall(document, candidate, linkTransform, level, wallTypes, shortCurveTolerance);
                        created[candidate] = wallId;
                        if (candidate.RevitElementId.HasValue)
                            report.Recreated++;
                    }
                    catch (Exception ex)
                    {
                        report.Failures.Add(candidate.Id + ": " + ex.Message);
                        LoggingUtils.Error("Wall for candidate " + candidate.Id + " was not created.", ex);
                    }
                }

                TransactionStatus status = transaction.Commit();
                report.SuppressedWarnings = swallower.DeletedWarnings;
                if (status != TransactionStatus.Committed)
                {
                    report.Failures.Add("The transaction did not commit (" + status + "). No walls were kept.");
                    return report;
                }
            }

            foreach (KeyValuePair<WallCandidate, ElementId> pair in created)
                pair.Key.RevitElementId = pair.Value.IntegerValue;

            report.Created = created.Count;
            report.WallTypesCreated.AddRange(wallTypes.CreatedNames);
            return report;
        }

        private ElementId CreateWall(
            Document document,
            WallCandidate candidate,
            Transform linkTransform,
            Level level,
            WallTypeCache wallTypes,
            double shortCurveTolerance)
        {
            if (!candidate.HeightMm.HasValue || candidate.HeightMm.Value <= 0.0)
                throw new InvalidOperationException("Approved candidate has no height.");

            XYZ start = _coordinates.CadMillimetersToRevitPoint(candidate.StartXMm, candidate.StartYMm, 0.0, linkTransform);
            XYZ end = _coordinates.CadMillimetersToRevitPoint(candidate.EndXMm, candidate.EndYMm, 0.0, linkTransform);
            double z = level.ProjectElevation;
            start = new XYZ(start.X, start.Y, z);
            end = new XYZ(end.X, end.Y, z);
            if (start.DistanceTo(end) <= shortCurveTolerance)
                throw new InvalidOperationException("Wall is shorter than Revit's minimum curve length.");

            WallType wallType = wallTypes.GetOrCreate(candidate.ThicknessMm);
            Wall wall = Wall.Create(
                document,
                Line.CreateBound(start, end),
                wallType.Id,
                level.Id,
                RevitUnitUtils.MillimetersToFeet(candidate.HeightMm.Value),
                0.0,
                false,
                false);

            return wall.Id;
        }

        private sealed class WallTypeCache
        {
            private readonly Document _document;
            private readonly Dictionary<string, WallType> _byName;
            private WallType _template;

            public WallTypeCache(Document document)
            {
                _document = document;
                _byName = new FilteredElementCollector(document)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .Where(t => t.Kind == WallKind.Basic)
                    .GroupBy(t => t.Name, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                CreatedNames = new List<string>();
            }

            public List<string> CreatedNames { get; private set; }

            public WallType GetOrCreate(double thicknessMm)
            {
                double widthMm = WallGeometryCheck.RoundedThicknessMm(thicknessMm);
                if (widthMm <= 0.0)
                    throw new InvalidOperationException("Wall thickness rounds to 0 mm.");

                string name = WallGeometryCheck.WallTypeName(thicknessMm);
                WallType existing;
                if (_byName.TryGetValue(name, out existing))
                    return existing;

                WallType template = Template();
                WallType created = (WallType)template.Duplicate(name);
                CompoundStructure structure = CompoundStructure.CreateSingleLayerCompoundStructure(
                    MaterialFunctionAssignment.Structure,
                    RevitUnitUtils.MillimetersToFeet(widthMm),
                    StructuralMaterial(template));
                created.SetCompoundStructure(structure);

                _byName[name] = created;
                CreatedNames.Add(name);
                return created;
            }

            private WallType Template()
            {
                if (_template != null)
                    return _template;

                List<WallType> basic = _byName.Values.Where(t => t.GetCompoundStructure() != null).ToList();
                _template = basic.FirstOrDefault(t => t.Name.IndexOf("Generic", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? basic.FirstOrDefault();
                if (_template == null)
                    throw new InvalidOperationException("The project has no basic wall type to copy.");

                return _template;
            }

            private static ElementId StructuralMaterial(WallType template)
            {
                CompoundStructure structure = template.GetCompoundStructure();
                if (structure == null)
                    return ElementId.InvalidElementId;

                foreach (CompoundStructureLayer layer in structure.GetLayers())
                {
                    if (layer.Function == MaterialFunctionAssignment.Structure)
                        return layer.MaterialId;
                }

                return ElementId.InvalidElementId;
            }
        }

        // Overlap and join warnings would otherwise stop the command with a modal dialog per wall.
        private sealed class WarningSwallower : IFailuresPreprocessor
        {
            public int DeletedWarnings { get; private set; }

            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (FailureMessageAccessor failure in failuresAccessor.GetFailureMessages())
                {
                    if (failure.GetSeverity() == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(failure);
                        DeletedWarnings++;
                    }
                }

                return FailureProcessingResult.Continue;
            }
        }
    }

    public class WallGenerationReport
    {
        public WallGenerationReport()
        {
            Failures = new List<string>();
            WallTypesCreated = new List<string>();
        }

        public int Created { get; set; }

        public int Recreated { get; set; }

        public int SkippedExisting { get; set; }

        public int SuppressedWarnings { get; set; }

        public List<string> Failures { get; private set; }

        public List<string> WallTypesCreated { get; private set; }

        public string Summary()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "created {0}, skipped existing {1}, failed {2}",
                Created,
                SkippedExisting,
                Failures.Count);
        }
    }
}
