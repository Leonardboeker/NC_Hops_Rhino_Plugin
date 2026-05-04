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

        // Default tool-database path. Same override scheme as the template.
        internal const string DefaultToolDbPathFallback =
            @"D:\Projekte\SynologyDrive\53_post-processor\reference-hops\werkzeug klemp.too";

        private const string TemplateEnvVar = "WALLABYHOP_TEMPLATE_PATH";
        private const string ToolDbEnvVar   = "WALLABYHOP_TOOLDB_PATH";
        private const string ConfigFileName = "WallabyHop.config.txt";

        // Cached after first lookup to avoid repeated I/O on every component evaluation
        private static string _cachedTemplatePath;
        private static string _cachedToolDbPath;

        /// <summary>
        /// The drawing template path, resolved from env-var → local config → APPDATA → default.
        /// </summary>
        internal static string DefaultTemplatePath
        {
            get
            {
                if (_cachedTemplatePath != null) return _cachedTemplatePath;
                _cachedTemplatePath = Resolve(TemplateEnvVar, "template", DefaultTemplatePathFallback);
                return _cachedTemplatePath;
            }
        }

        /// <summary>
        /// The tool database (.too) path, resolved with the same priority chain.
        /// </summary>
        internal static string DefaultToolDbPath
        {
            get
            {
                if (_cachedToolDbPath != null) return _cachedToolDbPath;
                _cachedToolDbPath = Resolve(ToolDbEnvVar, "tooldb", DefaultToolDbPathFallback);
                return _cachedToolDbPath;
            }
        }

        // Generic resolution: env var -> config file next to .gha -> APPDATA config -> hardcoded fallback.
        // Config file format is plain text "key = value" lines (or just a path on the first non-empty,
        // non-comment line for backward compatibility with the original single-purpose format).
        private static string Resolve(string envVarName, string configKey, string fallback)
        {
            try
            {
                string env = Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            }
            catch { /* fall through */ }

            try
            {
                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    string p = Path.Combine(asmDir, ConfigFileName);
                    string fromFile = ReadKeyOrFirstLine(p, configKey);
                    if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
                }
            }
            catch { /* fall through */ }

            try
            {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appdata))
                {
                    string p = Path.Combine(appdata, "Grasshopper", "Libraries", ConfigFileName);
                    string fromFile = ReadKeyOrFirstLine(p, configKey);
                    if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
                }
            }
            catch { /* fall through */ }

            return fallback;
        }

        // Look for "key = value" first; if no key match, fall back to the first non-comment line.
        // The fallback keeps backward compatibility with the v1 config format that only held the template path.
        private static string ReadKeyOrFirstLine(string path, string key)
        {
            if (!File.Exists(path)) return null;
            string firstLine = null;
            using (var sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = trimmed.Substring(0, eq).Trim();
                        string v = trimmed.Substring(eq + 1).Trim();
                        if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                            return v;
                    }
                    else if (firstLine == null)
                    {
                        firstLine = trimmed;
                    }
                }
            }
            // Fallback: only return a bare path if config contains no key=value entries at all
            // (so v1 single-line configs continue to work for the template path).
            return key == "template" ? firstLine : null;
        }
    }
}
