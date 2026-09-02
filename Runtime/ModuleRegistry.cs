namespace SiPVLib.Providers
{
    /// <summary>
    /// Known SiPVLib packages and their cross-module dependency graph, mirroring each package's
    /// own <c>package.json</c> dependencies (see the bootstrap's CLAUDE.md for the full graph).
    /// Extend this list as SiPVLib gains new packages.
    /// </summary>
    public static class ModuleRegistry
    {
        private const string RepoBaseUrl = "https://github.com/phajmvawnsix/";

        public static readonly ModuleDefinition[] All =
        {
            new ModuleDefinition
            {
                Id = "com.sipvlib.debugging",
                DisplayName = "Debugging",
                GitUrl = RepoBaseUrl + "com.sipvlib.debugging.git",
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.utilities",
                DisplayName = "Utilities",
                GitUrl = RepoBaseUrl + "com.sipvlib.utilities.git",
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.event",
                DisplayName = "Event",
                GitUrl = RepoBaseUrl + "com.sipvlib.event.git",
                DependsOnModuleIds = new[] { "com.sipvlib.debugging" },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.config",
                DisplayName = "Config",
                GitUrl = RepoBaseUrl + "com.sipvlib.config.git",
                DependsOnModuleIds = new[] { "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.utilities" },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.anticheat",
                DisplayName = "AntiCheat",
                GitUrl = RepoBaseUrl + "com.sipvlib.anticheat.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.utilities", "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.providers",
                DisplayName = "Providers",
                GitUrl = RepoBaseUrl + "com.sipvlib.providers.git",
                DependsOnModuleIds = new[] { "com.sipvlib.debugging" },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.pool",
                DisplayName = "Pool",
                GitUrl = RepoBaseUrl + "com.sipvlib.pool.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.utilities",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.userdata",
                DisplayName = "UserData",
                GitUrl = RepoBaseUrl + "com.sipvlib.userdata.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.event", "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.utilities",
                    "com.sipvlib.anticheat",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.ui",
                DisplayName = "UI",
                GitUrl = RepoBaseUrl + "com.sipvlib.ui.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.pool",
                    "com.sipvlib.utilities",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.sound",
                DisplayName = "Sound",
                GitUrl = RepoBaseUrl + "com.sipvlib.sound.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.userdata",
                    "com.sipvlib.utilities",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.vibrate",
                DisplayName = "Vibrate",
                GitUrl = RepoBaseUrl + "com.sipvlib.vibrate.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.userdata",
                    "com.sipvlib.utilities",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.tutorial",
                DisplayName = "Tutorial",
                GitUrl = RepoBaseUrl + "com.sipvlib.tutorial.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.config", "com.sipvlib.debugging", "com.sipvlib.event", "com.sipvlib.pool",
                    "com.sipvlib.ui", "com.sipvlib.userdata", "com.sipvlib.utilities",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.shop",
                DisplayName = "Shop",
                GitUrl = RepoBaseUrl + "com.sipvlib.shop.git",
                DependsOnModuleIds = new[]
                {
                    "com.sipvlib.event", "com.sipvlib.debugging", "com.sipvlib.config", "com.sipvlib.pool",
                    "com.sipvlib.userdata", "com.sipvlib.utilities", "com.sipvlib.ads",
                },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.extras.components",
                DisplayName = "Extras.Components",
                GitUrl = RepoBaseUrl + "com.sipvlib.extras.components.git",
                DependsOnModuleIds = new[] { "com.sipvlib.debugging", "com.sipvlib.sound", "com.sipvlib.vibrate" },
            },
            new ModuleDefinition
            {
                Id = "com.sipvlib.ads",
                DisplayName = "Ads",
                GitUrl = RepoBaseUrl + "com.sipvlib.ads.git",
                Notes = "Not cloned by clone-packages.sh in this bootstrap; only Shop depends on it.",
            },
        };

        public static ModuleDefinition Find(string id)
        {
            foreach (var module in All)
            {
                if (module.Id == id) return module;
            }

            return null;
        }
    }
}
