using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bibimcad.Models;
using Bibimcad.Services;
using Bibimcad.UI;
using Bibimcad.Utilities;

namespace Bibimcad.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdHelp : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null)
                    throw new ArgumentNullException("commandData");

                string statePath = PathUtils.GetProjectStatePath();
                HelpWindow window = new HelpWindow(
                    Diagnose(statePath),
                    HelpAdvisor.CommonProblems(),
                    statePath,
                    PathUtils.GetLogPath());
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                return BibimcadDialog.Fail(ref message, "Help failed.", ex);
            }
        }

        private static List<HelpTopic> Diagnose(string statePath)
        {
            if (!File.Exists(statePath))
                return HelpAdvisor.MissingStateFile(statePath);

            ProjectState state;
            try
            {
                state = new JsonProjectRepository(statePath).Load();
            }
            catch (Exception ex)
            {
                return HelpAdvisor.UnreadableStateFile(statePath, ex.Message);
            }

            if (state == null || !ProjectState.IsSupportedSchemaVersion(state.SchemaVersion))
            {
                return HelpAdvisor.UnreadableStateFile(
                    statePath,
                    "schemaVersion " + (state == null ? "(missing)" : state.SchemaVersion) + " is not supported. Copy the template again.");
            }

            state.UpgradeToCurrentSchema();
            return HelpAdvisor.Diagnose(state, File.Exists);
        }
    }
}
