using System;
using Autodesk.Revit.UI;
using Bibimcad.Utilities;

namespace Bibimcad.UI
{
    public static class BibimcadDialog
    {
        public static void Show(string instruction, string content)
        {
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = instruction;
            dialog.MainContent = content;
            dialog.Show();
        }

        public static bool Confirm(string instruction, string content)
        {
            TaskDialog dialog = new TaskDialog("Bibimcad");
            dialog.TitleAutoPrefix = false;
            dialog.MainInstruction = instruction;
            dialog.MainContent = content;
            dialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            dialog.AllowCancellation = true;
            return dialog.Show() == TaskDialogResult.Ok;
        }

        public static Result Fail(ref string message, string summary, Exception ex)
        {
            try
            {
                LoggingUtils.Error(summary, ex);
            }
            catch (Exception logEx)
            {
                message = summary + " " + ex.Message + " Log write failed: " + logEx.Message;
                Show(summary, message);
                return Result.Failed;
            }

            message = summary + " See the Bibimcad log.";
            Show(summary, ex.Message);
            return Result.Failed;
        }
    }
}
