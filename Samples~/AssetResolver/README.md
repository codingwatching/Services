# Asset Resolver (Sample)

A focused demo of `AssetResolverService` end-to-end: register a typed-config ScriptableObject, request an asset by id-enum, unload references when done.

> **Sample-only types**: `SpriteId`, `SpriteConfigs`, `AssetResolverExample`. These are NOT part of the `com.gamelovers.services` public API — they live in `GameLovers.Services.Samples.AssetResolver` to make that explicit.

This sample exercises the three Services Explorer tabs that the **Services Playground** sample doesn't cover: **Asset Resolver**, **Assets Importer**, and **Addressable Ids**. It also exercises the `AssetConfigsScriptableObject` custom inspector and its "Regenerate Addressable Ids" button.

---

## Why this sample requires Addressables

`AssetResolverService` is an Addressables wrapper, so anything this sample loads must be marked Addressable in your project. The sample ships with three placeholder sprites (`Sprites/Hero.png`, `Coin.png`, `Enemy.png`) and an empty `SpriteConfigs.asset`; an editor automation handles the wiring on your behalf the moment those sprites land in your project.

If you press Play before the automation finishes (or before you swap in your own sprites), the driver catches the `MissingMemberException` from `AssetResolverService` and surfaces a friendly status message via the on-screen `Status` text.

---

## Setup

The sample's editor automation (`AssetResolverSampleSetup`, fired by an `AssetPostprocessor`) does everything for you on import:

1. Marks every PNG under this sample's `Sprites/` folder as Addressable in a dedicated group `GameLoversServicesSamples_AssetResolver`. Never touches your default group or any other user-defined group.
2. Renames non-canonical filenames to `Hero` / `Coin` / `Enemy` (substring match first, alphabetical fallback). The shipped placeholders are already canonical, so first-import is a no-op.
3. Populates `SpriteConfigs.asset` rows for `SpriteId.Hero` / `Coin` / `Enemy`, pointing each at the matching sprite via `AssetReference`. **Existing user mappings are respected** — if a row already points at a different sprite, the automation skips it.
4. Logs a single summary line, e.g. `[AssetResolverSample] Setup complete. Group: 'GameLoversServicesSamples_AssetResolver', sprites in group: 3, configs entries set: 3, renamed: 0.`

If Addressables Settings don't exist yet, they are created (default location). The user's first sample import generates `AddressableAssetSettings.asset` and the sample's group together.

### Press Play

Open `AssetResolver.unity` and press Play. Click **Load Hero / Load Coin / Load Enemy** — the resolved sprite renders. **Unload All** releases the Addressables handles (references kept; LoadAsset would re-fetch).

### Swap in your own sprites

Drop your own PNGs into `Assets/Samples/GameLovers Services/<version>/Asset Resolver/Sprites/`. The post-processor fires on import:

- Files named `Hero.png` / `Coin.png` / `Enemy.png` (case-insensitive) are kept as-is.
- Files containing one of those words (e.g. `MyHeroIcon.png`) are renamed to the canonical name.
- Anything else is renamed to fill remaining slots in alphabetical order.

You don't need to modify `SpriteConfigs.asset` by hand — the automation re-runs and updates the rows.

### Re-running setup manually

Two escape hatches if you want to force a re-run (e.g., you deleted the Addressables group, replaced sprites while the editor was closed, or want to verify state):

- **Menu**: `Tools > GameLovers > Samples > Asset Resolver > Refresh Addressables`
- **Inspector button**: select `SpriteConfigs.asset` — the package's custom inspector adds a **Refresh AssetResolver Sample Addressables** button at the bottom (only visible when the inspected asset lives under `Asset Resolver/`).

### Manual fallback (no automation)

If you prefer to set things up by hand (or the automation doesn't apply to your workflow), use the four-step flow:

1. Pick or create three Sprite assets.
2. Mark them Addressable in `Window > Asset Management > Addressables > Groups`.
3. Open `SpriteConfigs.asset` and add three entries: `SpriteId.Hero/Coin/Enemy` → drag your sprites into the `AssetReference` slots.
4. Press Play.

---

## What the sample teaches

| Surface | Demo action |
|---|---|
| `AssetResolverService.AddConfigs<TId, TAsset>` | `Start()` registers `SpriteConfigs` so requests by `SpriteId` resolve |
| `IAssetResolverService.RequestAsset<TId, TAsset>` | Each "Load" button pulls a sprite by enum id; the result drives a uGUI Image |
| `IAssetResolverService.UnloadAssets<TId, TAsset>` | "Unload All" releases handles via the package's contract; references kept |
| `AssetConfigsScriptableObject<TId, TAsset>` custom inspector | Inspect `SpriteConfigs.asset` — the package ships a custom inspector with diagnostics (duplicate keys, empty GUIDs), a **Regenerate Addressable Ids** button, and (for this sample only) a **Refresh AssetResolver Sample Addressables** button |
| `AddressableIdsGeneratorUtils` | Use `Tools > GameLovers > Addressable Ids > Open in Explorer` to regenerate the `SpriteId` enum from your Addressables labels (the sample ships a hand-defined `SpriteId` so you can press Play before running the generator) |
| Services Explorer **Asset Resolver** tab | While Play is running, opens the live `AssetMap` tree (asset type → id type → id → ref + loaded status); per-asset Unload + bulk **Unload All** |
| Services Explorer **Assets Importer** tab | Discovered `IAssetConfigsImporter` list with per-importer paths and statuses |
| Services Explorer **Addressable Ids** tab | Generator settings, output status, **Generate Addressable Ids** + **Open Addressables Groups** |

---

## Troubleshooting

- **"The AssetResolverService does not have the AssetReference config to load …"** — the automation didn't run or `SpriteConfigs.asset` is still empty. Run `Tools > GameLovers > Samples > Asset Resolver > Refresh Addressables` and check the Console for the summary line.
- **"AssetReference resolved but Sprite was null"** — the entry points at an asset that isn't Addressable, or its group was deleted. Re-run the refresh menu (it re-creates the group) or check `Window > Asset Management > Addressables > Groups`.
- **"Menu 'Tools/GameLovers/Samples/Asset Resolver/Refresh Addressables' is unavailable"** — the sample's editor scripts aren't compiled (e.g. you deleted the sample's `Editor/` folder). Re-import the sample from Package Manager.
- **`MissingMethodException: ConfigConverter+Default constructor not found`** — unrelated to this sample. It's a Newtonsoft-JSON quirk; the services package's `DataService` doesn't run in this sample.

---

## Sibling sample

For the foundation services without Addressables, see the **Services Playground** sample.
