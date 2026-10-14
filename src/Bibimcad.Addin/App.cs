using System;
using Autodesk.Revit.UI;
using Bibimcad.UI;
using Bibimcad.Utilities;

namespace Bibimcad
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                RibbonBuilder.Build(application);
                LoggingUtils.Info("Bibimcad started.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                try
                {
                    LoggingUtils.Error("Bibimcad startup failed.", ex);
                }
                catch (Exception logEx)
                {
                    TaskDialog.Show(
                        "Bibimcad",
                        "Startup failed, and the log could not be written."
                        + Environment.NewLine
                        + Environment.NewLine
                        + ex.Message
                        + Environment.NewLine
                        + logEx.Message);
                }

                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            LoggingUtils.Info("Bibimcad shut down.");
            return Result.Succeeded;
        }
    }
}
