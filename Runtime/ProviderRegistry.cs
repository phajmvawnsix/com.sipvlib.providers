namespace SiPVLib.Providers
{
    /// <summary>
    /// Known third-party providers SiPVLib can integrate with. Extend this list as SiPVLib gains
    /// support for more ad networks or storage backends.
    /// </summary>
    public static class ProviderRegistry
    {
        public static readonly ProviderDefinition[] All =
        {
            new ProviderDefinition
            {
                Id = "admob",
                DisplayName = "Google AdMob",
                Category = ProviderCategory.Ads,
                PackageId = "com.google.ads.mobile",
                PackageVersion = "11.2.0",
                ScriptingDefineSymbol = null, // auto-derived by SiPV.Ads.asmdef's versionDefines
                InstallNotes = "Installed from Unity's package registry. SiPV.Ads auto-detects it " +
                                "(defines SIPV_ADS_ADMOB) once the package is present - no manual define needed.",
            },
            new ProviderDefinition
            {
                Id = "max",
                DisplayName = "AppLovin MAX",
                Category = ProviderCategory.Ads,
                PackageId = "com.applovin.max.unity",
                PackageVersion = "5.5.501",
                ScriptingDefineSymbol = null, // auto-derived by SiPV.Ads.asmdef's versionDefines
                InstallNotes = "Requires the AppLovin scoped registry (registry.npmjs.org, scope " +
                                "com.applovin) to already be present in Packages/manifest.json. " +
                                "SiPV.Ads auto-detects it (defines SIPV_ADS_MAX) once installed.",
            },
            new ProviderDefinition
            {
                Id = "levelplay",
                DisplayName = "Unity LevelPlay",
                Category = ProviderCategory.Ads,
                PackageId = "com.unity.services.levelplay",
                PackageVersion = "9.5.0",
                ScriptingDefineSymbol = null, // auto-derived by SiPV.Ads.asmdef's versionDefines
                InstallNotes = "Installed from Unity's package registry. SiPV.Ads auto-detects it " +
                                "(defines SIPV_ADS_LEVELPLAY) once the package is present.",
            },
            new ProviderDefinition
            {
                Id = "applovin_legacy",
                DisplayName = "AppLovin (legacy, via MAX SDK)",
                Category = ProviderCategory.Ads,
                PackageId = null, // no separate package - reuses the MAX SDK
                ScriptingDefineSymbol = "SIPV_ADS_APPLOVIN",
                DependsOnProviderIds = new[] { "max" },
                InstallNotes = "AppLovin discontinued its standalone SDK; this reuses the MAX SDK " +
                                "package. Requires the 'max' provider installed first. There is no " +
                                "package to add - this only toggles the SIPV_ADS_APPLOVIN define.",
            },
            new ProviderDefinition
            {
                Id = "firestore",
                DisplayName = "Firebase Firestore",
                Category = ProviderCategory.Storage,
                PackageId = null, // Firebase is consumed via Assets/Firebase asset-tree import in
                                   // this project, not a standard UPM registry package, so a simple
                                   // Package Manager add can't provision it reliably.
                ScriptingDefineSymbol = "FIREBASE_FIRESTORE",
                InstallNotes = "Requires manually importing the Firebase Firestore and Auth SDK " +
                                "modules (Firebase Unity SDK download + External Dependency Manager " +
                                "resolution), since Firebase isn't installed via a standard UPM " +
                                "registry in this project. Once imported, this toggle enables the " +
                                "FIREBASE_FIRESTORE define that SiPV.UserData's StorageProviderFirestore " +
                                "checks for.",
            },
        };

        public static ProviderDefinition Find(string id)
        {
            foreach (var provider in All)
            {
                if (provider.Id == id) return provider;
            }

            return null;
        }
    }
}
