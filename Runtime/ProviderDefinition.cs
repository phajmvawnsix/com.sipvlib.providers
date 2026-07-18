namespace SiPVLib.Providers
{
    /// <summary>
    /// Describes one third-party service provider that SiPVLib can integrate with
    /// (an ad network, a user-data storage backend, etc.).
    /// </summary>
    public class ProviderDefinition
    {
        /// <summary>Stable identifier, used as a lookup key. Never shown to the user.</summary>
        public string Id;

        public string DisplayName;
        public ProviderCategory Category;

        /// <summary>
        /// UPM package id to install/remove via Package Manager, or null if this provider has
        /// no package of its own (e.g. it reuses another provider's SDK, or its package can't be
        /// resolved through a standard UPM registry in this project).
        /// </summary>
        public string PackageId;

        /// <summary>Version to install when <see cref="PackageId"/> is set.</summary>
        public string PackageVersion;

        /// <summary>
        /// Scripting define symbol this provider manager must add/remove itself, or null if the
        /// symbol is already derived automatically from the package's presence via the consuming
        /// asmdef's own `versionDefines` (as is the case for AdMob/MAX/LevelPlay in SiPV.Ads).
        /// </summary>
        public string ScriptingDefineSymbol;

        /// <summary>Provider Ids that must already be installed before this one can be enabled.</summary>
        public string[] DependsOnProviderIds = System.Array.Empty<string>();

        /// <summary>Shown in the Provider Manager window; explains manual steps automation can't cover.</summary>
        public string InstallNotes;
    }
}
