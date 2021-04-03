using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace AstroModIntegrator
{
    public static class IntegratorUtils
    {
        public static Version CurrentVersion = new Version(1, 3, 1, 0);

        public static Regex GameRegex = new Regex(@"^\/Game\/", RegexOptions.Compiled);
        public static string ConvertGamePathToAbsolutePath(this string gamePath)
        {
            if (!GameRegex.IsMatch(gamePath)) return string.Empty;
            string newPath = GameRegex.Replace(gamePath, "Astro/Content/", 1);

            if (Path.HasExtension(newPath)) return newPath;
            return Path.ChangeExtension(newPath, ".uasset");
        }

        public static string GetEnumMemberAttrValue(this object enumVal)
        {
            EnumMemberAttribute attr = enumVal.GetType().GetMember(enumVal.ToString())[0].GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return attr?.Value;
        }
    }
}
