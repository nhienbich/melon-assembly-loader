using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader.Utils;

namespace MelonScriptLoader
{
    internal static class Utilities
    {
        public static Dictionary<string, string> KnownPaths { get; private set; }

        public static void Init(string scriptsPath)
        {
            KnownPaths = new Dictionary<string, string>
            {
                ["Managed"] = FindManaged(),
                ["Scripts"] = scriptsPath,
                ["MelonLoader"] = MelonEnvironment.MelonLoaderDirectory,
                ["UserLibs"] = MelonEnvironment.UserLibsDirectory,
                ["Mods"] = MelonEnvironment.ModsDirectory,
                ["UserData"] = MelonEnvironment.UserDataDirectory
            };
        }

        public static string Template(this string template, Dictionary<string, string> replacements)
        {
            var sb = new StringBuilder(template.Length);
            var sbTemplate = new StringBuilder();

            var insideTemplate = false;
            var bracedTemplate = false;
            for (var i = 0; i < template.Length; i++)
            {
                var c = template[i];
                switch (c)
                {
                    case '\\':
                        if (i + 1 < template.Length && template[i + 1] == '$')
                        {
                            sb.Append('$');
                            i++;
                            continue;
                        }

                        break;
                    case '$':
                        insideTemplate = true;
                        continue;
                    case '{':
                        if (insideTemplate)
                        {
                            bracedTemplate = true;
                            continue;
                        }

                        break;
                    case '}':
                        if (insideTemplate && sbTemplate.Length > 0)
                        {
                            string replacement;
                            if (replacements.TryGetValue(sbTemplate.ToString(), out replacement))
                                sb.Append(replacement);
                            sbTemplate.Length = 0;
                            insideTemplate = false;
                            bracedTemplate = false;
                            continue;
                        }

                        break;
                }

                if (insideTemplate && !bracedTemplate && !char.IsDigit(c))
                {
                    string replacement;
                    if (replacements.TryGetValue(sbTemplate.ToString(), out replacement))
                        sb.Append(replacement);
                    sbTemplate.Length = 0;
                    insideTemplate = false;
                }

                if (insideTemplate)
                    sbTemplate.Append(c);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static string FindManaged()
        {
            var root = MelonEnvironment.GameRootDirectory;
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var managed = Path.Combine(dir, "Managed");
                    if (Directory.Exists(managed))
                        return managed;
                }
            }

            return Path.Combine(MelonEnvironment.MelonLoaderDirectory, "Managed");
        }
    }
}
