# Changelog

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
