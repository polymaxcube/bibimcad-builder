using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Bibimcad.Utilities;

namespace Bibimcad.Services
{
    public static class CadLinkLocator
    {
        public static List<ImportInstance> FindLinkedMatches(Document document, string dwgName)
        {
            List<ImportInstance> matches = new List<ImportInstance>();
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ImportInstance));

            foreach (Element element in collector)
            {
                ImportInstance importInstance = element as ImportInstance;
                if (importInstance == null || !importInstance.IsValidObject || !importInstance.IsLinked)
                    continue;

                string cadName = ElementNameUtils.GetCadName(document, importInstance);
                if (ElementNameUtils.NamesMatch(cadName, dwgName))
                    matches.Add(importInstance);
            }

            return matches;
        }

        public static string Describe(Document document, ImportInstance importInstance)
        {
            string cadName = ElementNameUtils.GetCadName(document, importInstance);
            if (string.IsNullOrWhiteSpace(cadName))
                cadName = "(name unresolved)";

            return cadName
                + "  |  Id "
                + importInstance.Id.IntegerValue.ToString(CultureInfo.InvariantCulture);
        }
    }
}
