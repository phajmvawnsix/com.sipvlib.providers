# Changelog

## [1.3.0] - 2026-09-02

The Modules window now manages SiPVLib packages as UPM git dependencies in
`Packages/manifest.json` rather than as folders under `Assets/SiPVLib` — the manifest is how a
consuming project actually pulls SiPVLib in, and `Assets/SiPVLib` only exists in the bootstrap
project. Install/Update/Remove go through Package Manager (`Client.Add`/`Client.Remove`) instead of
shelling out to `git clone`.

Each row now shows the installed version alongside the latest published GitHub release, flags when
an update is available, and offers a **Changelog** button that fetches that release's `CHANGELOG.md`
from GitHub. Remote versions come from `git ls-remote --tags` (highest semver wins) and changelogs
from raw.githubusercontent.com; both are fetched off `EditorApplication.update` so neither blocks
the Editor.

## [1.2.1] - 2026-09-02

Editor performance fixes:

- `ProviderManagerService.GetInstalledPackageIds` blocked the main thread on a
  `while (!request.IsCompleted) Thread.Sleep(10)` loop, reached from `OnGUI`. Since `InvalidateCache`
  nulls the cache after every package operation, the next repaint froze the Editor until Package
  Manager answered. The `ListRequest` is now polled alongside the other pending requests; the window
  shows "Checking..." and disables its actions until the listing lands.
- `ModuleManagerService.IsModuleInstalled` hit `Directory.Exists` several times per module per
  repaint. Now cached, invalidated on install/remove, on window enable, and via a new Refresh button.

## [1.2.0] - 2026-09-02

Add **SiPV > Modules** window (`ModuleManagerWindow`/`ModuleManagerService`/`ModuleRegistry`/`ModuleDefinition`):
install/update/remove SiPVLib packages themselves (`Assets/SiPVLib/<module-id>`) via `git`, since they're
independent repos cloned into the project rather than UPM registry packages. Install pulls in a module's
missing dependencies first (foundation packages before dependents); Remove refuses if another installed
module still depends on it.

## [1.1.0] - 2026-09-01

`ProviderManagerWindow` now uses tabs, one per module category, instead of one long scrolling list.
Adds `ProviderCategory.Config` and a Firebase Remote Config provider entry.

## [1.0.0] - 2026-07-19

Initial release. Manages AdMob, AppLovin MAX, Unity LevelPlay, legacy AppLovin, and Firebase Firestore providers.
