# Changelog

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
