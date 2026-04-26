# Asset Resolver (Sample)

A focused demo of `AssetResolverService` end-to-end: register a typed-config ScriptableObject, request an asset by id-enum, unload references when done.

> **Sample-only types**: `SpriteId`, `SpriteConfigs`, `AssetResolverExample`. These are NOT part of the `com.gamelovers.services` public API — they live in `GameLovers.Services.Samples.AssetResolver` to make that explicit.

This sample exercises the three Services Explorer tabs that the **Services Playground** sample doesn't cover: **Asset Resolver**, **Assets Importer**, and **Addressable Ids**. It also exercises the `AssetConfigsScriptableObject` custom inspector and its "Regenerate Addressable Ids" button.

---

## Why this sample is not zero-setup

Unlike `ServicesPlayground`, this sample requires Addressables to actually do anything — `AssetResolverService` is an Addressables wrapper. Marking sprites as Addressable + populating `SpriteConfigs` is per-project state, so the sample can ship the wiring (scene, driver, ScriptableObject, scripts) but can't ship your sprites or your Addressables groups.

If you press Play before completing setup, the driver catches the `MissingMemberException` from `AssetResolverService` and surfaces a friendly status message telling you the next step.

---

## Setup (~2 minutes)

You need three Addressable `Sprite` assets and a `SpriteConfigs.asset` that maps `SpriteId` values to them.

### 1. Pick (or create) three Sprites

Anything works — drop three PNGs into your project, or use Unity's built-in UI/skin sprites. The sample doesn't care about content, only that they're Addressable.

### 2. Mark them Addressable

Open `Window > Asset Management > Addressables > Groups`. Drag your three sprites into the default group (or any group). Optionally apply a label like `samples-sprites`.

> No Addressables Settings yet? Click **Create Addressables Settings**. The default group is fine.

### 3. Wire them into `SpriteConfigs.asset`

1. Select `SpriteConfigs.asset` in this sample folder (`Assets/Samples/GameLovers Services/<version>/Asset Resolver/SpriteConfigs.asset`).
2. In the inspector, expand `_configs` (or use the package's custom inspector if shown).
3. Add three entries:

| Key | Value (drag your sprite here) |
|---|---|
| `SpriteId.Hero` | sprite #1 |
| `SpriteId.Coin` | sprite #2 |
| `SpriteId.Enemy` | sprite #3 |

The `Value` field is an `AssetReference` — you can only drop assets that are already Addressable.

### 4. Press Play

Open `AssetResolver.unity` and press Play. Click **Load Hero / Load Coin / Load Enemy** — the resolved Sprite displays. **Unload All** releases the Addressables handles (references are kept; LoadAsset would re-fetch).

---

## What the sample teaches

| Surface | Demo action |
|---|---|
| `AssetResolverService.AddConfigs<TId, TAsset>` | `Start()` registers `SpriteConfigs` so requests by `SpriteId` resolve |
| `IAssetResolverService.RequestAsset<TId, TAsset>` | Each "Load" button pulls a sprite by enum id; the result drives a uGUI Image |
| `IAssetResolverService.UnloadAssets<TId, TAsset>` | "Unload All" releases handles via the package's contract; references kept |
| `AssetConfigsScriptableObject<TId, TAsset>` custom inspector | Inspect `SpriteConfigs.asset` — the package ships a custom inspector with diagnostics (duplicate keys, empty GUIDs) and a **Regenerate Addressable Ids** button |
| `AddressableIdsGeneratorUtils` | Use `Tools > GameLovers > Addressable Ids > Open in Explorer` to regenerate the `SpriteId` enum from your Addressables labels (the sample ships a hand-defined `SpriteId` so you can press Play before running the generator) |
| Services Explorer **Asset Resolver** tab | While Play is running, opens the live `AssetMap` tree (asset type → id type → id → ref + loaded status); per-asset Unload + bulk **Unload All** |
| Services Explorer **Assets Importer** tab | Discovered `IAssetConfigsImporter` list with per-importer paths and statuses |
| Services Explorer **Addressable Ids** tab | Generator settings, output status, **Generate Addressable Ids** + **Open Addressables Groups** |

---

## Troubleshooting

- **"The AssetResolverService does not have the AssetReference config to load …"** — the configs aren't registered yet. Double-check that `SpriteConfigs.asset` has the three entries (Step 3) and that you haven't cleared Play mode after `Start()` ran.
- **"AssetReference resolved but Sprite was null"** — the reference points at an asset that isn't Addressable, or its label / group was deleted. Re-check Addressables Groups (Step 2).
- **`MissingMethodException: ConfigConverter+Default constructor not found`** — unrelated to this sample. It's a Newtonsoft-JSON quirk; the services package's `DataService` doesn't run in this sample.

---

## Sibling sample

For the foundation services without Addressables, see the **Services Playground** sample.
