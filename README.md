# com.sipvlib.providers

Part of [SiPVLib](https://github.com/phajmvawnsix/SiPVLib). A single place to add, remove, and (where supported) update the third-party providers SiPVLib integrates with — ad networks used by `com.sipvlib.ads`, and storage backends used by `com.sipvlib.userdata`. Open it from the Unity menu: **SiPV > Providers**.

If a provider isn't added, its related code across SiPVLib stays behind a scripting define and is excluded from compilation/builds — the same pattern already used for Odin Inspector (`ODIN_INSPECTOR`) and Solo MOST_IN_ONE (`SIPV_VIBRATE_MOST_IN_ONE`) elsewhere in SiPVLib, just automated here.

## Install

Add to your project's `Packages/manifest.json`:

```json
"com.sipvlib.providers": "https://github.com/phajmvawnsix/com.sipvlib.providers.git",
"com.sipvlib.debugging": "https://github.com/phajmvawnsix/com.sipvlib.debugging.git"
```

UPM does not automatically resolve nested git dependencies — add `com.sipvlib.debugging` yourself alongside this package.

## Supported providers

| Provider | Category | Package | Define | Notes |
|---|---|---|---|---|
| Google AdMob | Ads | `com.google.ads.mobile` | `SIPV_ADS_ADMOB` (auto) | Installed/removed via Package Manager; `com.sipvlib.ads`'s own `versionDefines` picks up the define automatically. |
| AppLovin MAX | Ads | `com.applovin.max.unity` | `SIPV_ADS_MAX` (auto) | Requires the AppLovin scoped registry already present in `manifest.json`. |
| Unity LevelPlay | Ads | `com.unity.services.levelplay` | `SIPV_ADS_LEVELPLAY` (auto) | Covers the LevelPlay/ironSource-successor SDK. |
| AppLovin (legacy) | Ads | *(none — reuses MAX SDK)* | `SIPV_ADS_APPLOVIN` (manual) | Requires the MAX provider installed first; this only toggles the define. |
| Firebase Firestore | Storage | *(none — manual import)* | `FIREBASE_FIRESTORE` (manual) | Firebase isn't consumed via a standard UPM registry in this project's setup (asset-tree import + External Dependency Manager), so the package can't be auto-installed here; import the Firebase Firestore + Auth SDK modules yourself, then use this window to toggle the define. |

## How "Install"/"Remove" work

- **Package-backed providers** (AdMob, MAX, LevelPlay): calls `UnityEditor.PackageManager.Client.Add`/`Client.Remove`. The associated `SIPV_ADS_*` define is derived automatically by `com.sipvlib.ads`'s own `versionDefines` on the next domain reload — this window doesn't write it directly.
- **Define-only providers** (AppLovin legacy, Firebase Firestore): no package to install (either there's no separate SDK, or it can't be resolved through a standard UPM registry in this project); the window directly adds/removes the scripting define via `PlayerSettings.SetScriptingDefineSymbols` for the currently selected build target group.

## Related packages

`com.sipvlib.ads` and `com.sipvlib.userdata` depend on this package — see their own READMEs for what code each provider gates.
