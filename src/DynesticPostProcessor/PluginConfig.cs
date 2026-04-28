using System;
using System.IO;
using System.Reflection;

namespace WallabyHop
{
    /// <summary>
    /// External plugin configuration. Resolves user-overridable settings
    /// (e.g. drawing template path) from a config file or environment
    /// variable, falling back to a documented default.
    ///
    /// Resolution order for the template path:
    ///   1. Environment variable WALLABYHOP_TEMPLATE_PATH
    ///   2. config.txt next to the .gha plugin file (single line: the path)
    ///   3. %APPDATA%\Grasshopper\Libraries\WallabyHop.config.txt
    ///   4. Hardcoded default (Leo's E: drive layout — used as initial value
    ///      so existing setups keep working unchanged)
    /// </summary>
    internal static class PluginConfig
    {
        // The original hardcoded path. Kept here as a single source of truth.
        // Other rechners override via env-var or config.txt without changing code.
        internal const string DefaultTemplatePathFallback =
            @"E:\Rhino Resourcen\Plan Köpfe\Leonard Elias Böker.3dm";

        private const string EnvVarName = "WALLABYHOP_TEMPLATE_PATH";
        private const string ConfigFileName = "WallabyHop.config.txt";

        // Cached after first lookup to avoid repeated I/O on every component evaluation
        private static string _cachedTemplatePath;

        /// <summary>
        /// The drawing template path, resolved from env-var → local config → APPDATA → default.
        /// </summary>
        internal static string DefaultTemplatePath
        {
            get
            {
                if (_cachedTemplatePath != null) return _cachedTemplatePath;
                _cachedTemplatePath = ResolveTemplatePath();
                return _cachedTemplatePath;
            }
        }

        private static string ResolveTemplatePath()
        {
            // 1. Environment variable
            try
            {
                string env = Environment.GetEnvironmentVariable(EnvVarName);
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            }
            catch { /* fall through */ }

            // 2. Config file next to the .gha
            try
            {
                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    string p = Path.Combine(asmDir, ConfigFileName);
                    string fromFile = ReadFirstLine(p);
                    if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
                }
            }
            catch { /* fall through */ }

            // 3. Config file in APPDATA Libraries dir
            try
            {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appdata))
                {
                    string p = Path.Combine(appdata, "Grasshopper", "Libraries", ConfigFileName);
                    string fromFile = ReadFirstLine(p);
                    if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
                }
            }
            catch { /* fall through */ }

            // 4. Default
            return DefaultTemplatePathFallback;
        }

        private static string ReadFirstLine(string path)
        {
            if (!File.Exists(path)) return null;
            using (var sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0 && !trimmed.StartsWith("#")) return trimmed;
                }
            }
            return null;
        }
    }
}
