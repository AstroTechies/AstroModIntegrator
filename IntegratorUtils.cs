using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace AstroModIntegrator
{
    public static class IntegratorUtils
    {
        public static Regex GameRegex = new Regex(@"^\/Game\/", RegexOptions.Compiled);
        public static string ConvertGamePathToAbsolutePath(this string gamePath)
        {
            if (!GameRegex.IsMatch(gamePath)) return string.Empty;
            return Path.ChangeExtension(GameRegex.Replace(gamePath, "Astro/Content/", 1), ".uasset");
        }

        public static string GetEnumMemberAttrValue(this object enumVal)
        {
            EnumMemberAttribute attr = enumVal.GetType().GetMember(enumVal.ToString())[0].GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return attr?.Value;
        }
    }
}
