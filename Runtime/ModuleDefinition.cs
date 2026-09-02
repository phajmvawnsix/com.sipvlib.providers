namespace SiPVLib.Providers
{
    /// <summary>
    /// Describes one SiPVLib package (a folder under <c>Assets/SiPVLib/</c>, cloned from its own
    /// GitHub repo with <c>.git</c> preserved — not a standard UPM registry package).
    /// </summary>
    public class ModuleDefinition
    {
        /// <summary>Package id, matches both the folder name under Assets/SiPVLib and the repo name.</summary>
        public string Id;

        public string DisplayName;

        /// <summary>Git clone URL, or null if this module isn't managed by this bootstrap (see notes).</summary>
        public string GitUrl;

        /// <summary>Module Ids that must be installed before/alongside this one.</summary>
        public string[] DependsOnModuleIds = System.Array.Empty<string>();

        /// <summary>Shown in the Modules window; explains anything automation can't cover.</summary>
        public string Notes;
    }
}
