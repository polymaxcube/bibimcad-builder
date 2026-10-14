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
        private const string InspectButtonId = "Bibimcad_Inspect";
        private const string CoordinatesButtonId = "Bibimcad_Coordinates";

        public static void Build(UIControlledApplication application)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            EnsureTab(application);
            RibbonPanel panel = EnsurePanel(application);
            string assemblyPath = GetAssemblyPath();

            EnsureButton(
                panel,
                CreateButton(
                    InspectButtonId,
                    "01\nInspect",
                    assemblyPath,
                    "Bibimcad.Commands.CmdInspectProject",
                    "Read the central Bibimcad project state."));

            EnsureButton(
                panel,
                CreateButton(
                    CoordinatesButtonId,
                    "02\nCoordinates",
                    assemblyPath,
                    "Bibimcad.Commands.CmdCheckCoordinates",
                    "Read the linked CAD transform and save it to the project state."));
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
    }
}
