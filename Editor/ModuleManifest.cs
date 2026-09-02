using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Reads the project's <c>Packages/manifest.json</c> to determine which SiPVLib packages are
    /// declared as UPM git dependencies. This is the source of truth for "is this module installed"
    /// — a consuming project pulls SiPVLib in through the manifest, not as folders under Assets.
    /// </summary>
    public static class ModuleManifest
    {
        /// <summary>Matches one <c>"package.id": "value"</c> pair inside the dependencies block.</summary>
        private static readonly Regex DependencyPattern =
            new(@"""(?<id>[A-Za-z0-9_.\-]+)""\s*:\s*""(?<value>[^""]*)""", RegexOptions.Compiled);

        /// <summary>Pulls the <c>#tag</c> / <c>#branch</c> fragment off a git dependency URL.</summary>
        private static readonly Regex GitFragmentPattern =
            new(@"#(?<fragment>[^#?]+)$", RegexOptions.Compiled);

        public static string ManifestPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));

        /// <summary>
        /// Returns every dependency declared in the manifest, keyed by package id. Values are the
        /// raw manifest values (a version string for registry packages, a URL for git packages).
        /// </summary>
        public static Dictionary<string, string> ReadDependencies()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            var path = ManifestPath;
            if (!File.Exists(path)) return result;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception)
            {
                return result;
            }

            // Scope the scan to the "dependencies" object so unrelated top-level keys
            // (scopedRegistries, testables, ...) can't be mistaken for packages.
            var dependenciesBody = ExtractDependenciesBody(text);
            if (string.IsNullOrEmpty(dependenciesBody)) return result;

            foreach (Match match in DependencyPattern.Matches(dependenciesBody))
            {
                result[match.Groups["id"].Value] = match.Groups["value"].Value;
            }

            return result;
        }

        /// <summary>Returns the substring of the manifest holding the dependencies object's body.</summary>
        private static string ExtractDependenciesBody(string manifestText)
        {
            var keyIndex = manifestText.IndexOf("\"dependencies\"", StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var openIndex = manifestText.IndexOf('{', keyIndex);
            if (openIndex < 0) return null;

            var depth = 0;
            for (var i = openIndex; i < manifestText.Length; i++)
            {
                if (manifestText[i] == '{') depth++;
                else if (manifestText[i] == '}')
                {
                    depth--;
                    if (depth == 0) return manifestText.Substring(openIndex + 1, i - openIndex - 1);
                }
            }

            return null;
        }

        /// <summary>The version/tag a git dependency is pinned to, or null when it tracks the default branch.</summary>
        public static string GetPinnedFragment(string manifestValue)
        {
            if (string.IsNullOrEmpty(manifestValue)) return null;

            var match = GitFragmentPattern.Match(manifestValue);
            return match.Success ? match.Groups["fragment"].Value : null;
        }

        /// <summary>Builds the manifest value for a module, optionally pinned to <paramref name="version"/>.</summary>
        public static string BuildGitUrl(ModuleDefinition module, string version = null)
        {
            return string.IsNullOrEmpty(version) ? module.GitUrl : $"{module.GitUrl}#{version}";
        }
    }
}
