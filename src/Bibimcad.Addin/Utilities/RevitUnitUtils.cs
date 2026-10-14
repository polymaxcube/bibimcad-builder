using Autodesk.Revit.DB;

namespace Bibimcad.Utilities
{
    public static class RevitUnitUtils
    {
        public static double FeetToMillimeters(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }

        public static double MillimetersToFeet(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }
    }
}
