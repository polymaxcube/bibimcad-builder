using System;
using System.IO;
using Autodesk.Revit.DB;

namespace Bibimcad.Utilities
{
    public static class ElementNameUtils
    {
        private static readonly BuiltInParameter[] NameParameters = new[]
        {
            BuiltInParameter.ALL_MODEL_TYPE_NAME,
            BuiltInParameter.SYMBOL_NAME_PARAM,
            BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
            BuiltInParameter.ALL_MODEL_FAMILY_NAME
        };

        public static string GetCadName(Document document, ImportInstance importInstance)
        {
            if (document == null || importInstance == null)
                return string.Empty;

            try
            {
                ElementId typeId = importInstance.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    Element typeElement = document.GetElement(typeId);
                    string typeName = ReadElementName(typeElement);
                    if (!string.IsNullOrWhiteSpace(typeName))
                        return typeName;
                }

                return ReadElementName(importInstance);
            }
            catch (Exception ex)
            {
                LoggingUtils.Info("CAD name could not be resolved. " + ex.Message);
                return string.Empty;
            }
        }

        public static bool NamesMatch(string cadName, string dwgName)
        {
            if (string.IsNullOrWhiteSpace(cadName) || string.IsNullOrWhiteSpace(dwgName))
                return false;

            string expectedFile = SafeFileName(dwgName.Trim());
            string expectedStem = Path.GetFileNameWithoutExtension(expectedFile);
            string[] tokens = Tokenize(cadName);

            for (int i = 0; i < tokens.Length; i++)
            {
                string tokenFile = SafeFileName(tokens[i]);
                string tokenStem = Path.GetFileNameWithoutExtension(tokenFile);

                if (string.Equals(tokenFile, expectedFile, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrEmpty(expectedStem)
                    && string.Equals(tokenStem, expectedStem, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadElementName(Element element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                if (!string.IsNullOrWhiteSpace(element.Name))
                    return element.Name;
            }
            catch (Exception ex)
            {
                LoggingUtils.Info("Element.Name was not available. " + ex.Message);
            }

            return ReadNameParameter(element);
        }

        private static string ReadNameParameter(Element element)
        {
            for (int i = 0; i < NameParameters.Length; i++)
            {
                Parameter parameter = element.get_Parameter(NameParameters[i]);
                if (parameter == null || !parameter.HasValue)
                    continue;

                string value = null;
                if (parameter.StorageType == StorageType.String)
                    value = parameter.AsString();

                if (string.IsNullOrWhiteSpace(value))
                    value = parameter.AsValueString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string[] Tokenize(string value)
        {
            char[] separators = new char[] { ' ', '\t', ':', '|', '\\', '/', '<', '>' };
            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                string fileName = Path.GetFileName(value);
                return string.IsNullOrEmpty(fileName) ? value : fileName;
            }
            catch (ArgumentException)
            {
                return value;
            }
        }
    }
}
