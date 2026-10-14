using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.UI;
using Bibimcad.Utilities;

namespace Bibimcad.UI
{
    public static class RibbonBuilder
    {
        private const string TabName = "Bibimcad";
        private const string PanelName = "K1 Workflow";
        private static readonly ButtonSpec[] Buttons = new[]
        {
            new ButtonSpec(
                "Bibimcad_Inspect",
                "01\nInspect",
                "Bibimcad.Commands.CmdInspectProject",
                "Read the central Bibimcad project state."),
            new ButtonSpec(
                "Bibimcad_Coordinates",
                "02\nCoordinates",
                "Bibimcad.Commands.CmdCheckCoordinates",
                "Read the linked CAD transform and save it to the project state."),
            new ButtonSpec(
                "Bibimcad_ReadDxf",
                "03\nRead DXF",
                "Bibimcad.Commands.CmdReadDxf",
                "Read cadSource.dxfPath and save layers, blocks, and entity counts to cadAudit."),
            new ButtonSpec(
                "Bibimcad_Classify",
                "04\nClassify",
                "Bibimcad.Commands.CmdClassifyWalls",
                "Pair parallel lines on cadSource.wallLayers into wall candidates for review."),
            new ButtonSpec(
                "Bibimcad_Review",
                "05\nReview",
                "Bibimcad.Commands.CmdReviewWalls",
                "Approve or reject wall candidates and set their heights. Also exports and imports wall_review.csv."),
            new ButtonSpec(
                "Bibimcad_Generate",
                "06\nGenerate",
                "Bibimcad.Commands.CmdGenerateWalls",
                "Create Revit walls for approved candidates through the live CAD link transform. Existing walls are skipped."),
            new ButtonSpec(
                "Bibimcad_Validate",
                "07\nValidate",
                "Bibimcad.Commands.CmdValidateWalls",
                "Check each approved wall's endpoints, length, width, and height within 1 mm and save the result."),
        };

        public static void Build(UIControlledApplication application)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            EnsureTab(application);
            RibbonPanel panel = EnsurePanel(application);
            string assemblyPath = GetAssemblyPath();

            foreach (ButtonSpec button in Buttons)
            {
                EnsureButton(
                    panel,
                    CreateButton(button.Id, button.Text, assemblyPath, button.ClassName, button.ToolTip));
            }
        }

        private static void EnsureTab(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                LoggingUtils.Info("Ribbon tab already exists: " + TabName);
            }
        }

        private static RibbonPanel EnsurePanel(UIControlledApplication application)
        {
            IList<RibbonPanel> panels = application.GetRibbonPanels(TabName);
            foreach (RibbonPanel panel in panels)
            {
                if (string.Equals(panel.Name, PanelName, StringComparison.Ordinal))
                    return panel;
            }

            return application.CreateRibbonPanel(TabName, PanelName);
        }

        private static void EnsureButton(RibbonPanel panel, PushButtonData buttonData)
        {
            if (ContainsItem(panel, buttonData.Name))
                return;

            panel.AddItem(buttonData);
        }

        private static bool ContainsItem(RibbonPanel panel, string itemName)
        {
            foreach (RibbonItem item in panel.GetItems())
            {
                if (string.Equals(item.Name, itemName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static PushButtonData CreateButton(
            string id,
            string text,
            string assemblyPath,
            string className,
            string toolTip)
        {
            PushButtonData buttonData = new PushButtonData(id, text, assemblyPath, className);
            buttonData.ToolTip = toolTip;
            return buttonData;
        }

        private static string GetAssemblyPath()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                throw new InvalidOperationException(
                    "Bibimcad could not resolve its own assembly path.");
            }

            return assemblyPath;
        }

        private sealed class ButtonSpec
        {
            public ButtonSpec(string id, string text, string className, string toolTip)
            {
                Id = id;
                Text = text;
                ClassName = className;
                ToolTip = toolTip;
            }

            public string Id { get; private set; }

            public string Text { get; private set; }

            public string ClassName { get; private set; }

            public string ToolTip { get; private set; }
        }
    }
}
