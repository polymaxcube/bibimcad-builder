using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Bibimcad.Services;
using Bibimcad.Utilities;

namespace Bibimcad.UI
{
    // Chooses the linked CAD ImportInstance: the single match by name, a list for two to four matches,
    // otherwise a manual pick in the model.
    public static class CadLinkPicker
    {
        private const int MaxCommandLinks = 4;

        public static bool TryResolve(
            UIDocument uiDocument,
            Document document,
            string dwgName,
            out ImportInstance importInstance)
        {
            importInstance = null;
            List<ImportInstance> matches = CadLinkLocator.FindLinkedMatches(document, dwgName);
            LoggingUtils.Info(
                "Linked CAD matches for '"
                + dwgName
                + "': "
                + matches.Count.ToString(CultureInfo.InvariantCulture)
                + ".");

            if (matches.Count == 1)
            {
                importInstance = matches[0];
                LoggingUtils.Info("Using the single matching CAD link " + CadLinkLocator.Describe(document, importInstance) + ".");
                return true;
            }

            if (matches.Count > 1)
            {
                if (matches.Count <= MaxCommandLinks)
                    return TryChooseFromList(document, dwgName, matches, out importInstance);

                if (!BibimcadDialog.Confirm(
                    "Several CAD links match.",
                    "More than four linked CAD files match " + dwgName + ". Select the correct ImportInstance."))
                {
                    return false;
                }

                importInstance = PickImportInstance(
                    uiDocument,
                    document,
                    "Select the linked CAD file named " + dwgName + ".");
                return true;
            }

            if (!BibimcadDialog.Confirm(
                "No matching CAD link was found.",
                "Bibimcad looked for a linked CAD file named " + dwgName + ". Select an ImportInstance manually."))
            {
                return false;
            }

            importInstance = PickImportInstance(
                uiDocument,
                document,
                "Select a CAD import. No linked file named " + dwgName + " was found.");
            LoggingUtils.Info("Using manually selected CAD " + CadLinkLocator.Describe(document, importInstance) + ".");
            return true;
        }

        private static bool TryChooseFromList(
            Document document,
            string dwgName,
            IList<ImportInstance> matches,
            out ImportInstance importInstance)
        {
            importInstance = null;
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = "Select the linked CAD file.";
            dialog.MainContent = "More than one linked CAD file matches " + dwgName + ".";
            dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
            dialog.AllowCancellation = true;

            for (int i = 0; i < matches.Count; i++)
            {
                TaskDialogCommandLinkId linkId = (TaskDialogCommandLinkId)((int)TaskDialogCommandLinkId.CommandLink1 + i);
                dialog.AddCommandLink(linkId, CadLinkLocator.Describe(document, matches[i]));
            }

            TaskDialogResult dialogResult = dialog.Show();
            if (dialogResult == TaskDialogResult.Cancel || dialogResult == TaskDialogResult.Close)
                return false;

            int index = (int)dialogResult - (int)TaskDialogCommandLinkId.CommandLink1;
            if (index < 0 || index >= matches.Count)
                return false;

            importInstance = matches[index];
            LoggingUtils.Info("User selected CAD link " + CadLinkLocator.Describe(document, importInstance) + ".");
            return true;
        }

        private static ImportInstance PickImportInstance(UIDocument uiDocument, Document document, string prompt)
        {
            Reference reference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new ImportInstanceSelectionFilter(),
                prompt);

            Element element = document.GetElement(reference);
            ImportInstance importInstance = element as ImportInstance;
            if (importInstance == null)
                throw new InvalidOperationException("The selected element is not a CAD import.");

            return importInstance;
        }

        private sealed class ImportInstanceSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is ImportInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
