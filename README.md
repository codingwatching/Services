# GameLovers Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/github/v/tag/CoderGamester/Services?label=version)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [When to use](#when-to-use) | [Quick Start](#quick-start) | [Services](#services-at-a-glance) | [Docs](docs/README.md) | [Changelog](CHANGELOG.md) | [Migration Guide](MIGRATION.md)

## Why Use This Package?

Building robust game architecture in Unity often leads to tightly coupled systems, scattered initialization logic, and memory management headaches. This **Services** package solves these pain points:

| Problem | Solution |
|---------|----------|
| **Scattered dependencies** | Lightweight service locator (`MainInstaller`) for centralized dependency management |
| **Tightly coupled systems** | Message broker enables decoupled pub/sub communication |
| **Manual update management** | Tick service centralizes Update/FixedUpdate/LateUpdate callbacks |
| **Coroutines in pure C#** | Coroutine service runs Unity coroutines without MonoBehaviour |
| **Memory churn from instantiation** | Object pooling with lifecycle hooks for efficient reuse |
| **Inconsistent save/load** | Cross-platform data persistence with automatic serialization |
| **Non-deterministic gameplay** | Deterministic RNG service with state save/restore |
| **Version tracking complexity** | Build version service with git commit/branch metadata |

**Built for production:** Minimal per-frame allocations. Used in real games.

---

## When to use

**Use this package when** you want a lightweight set of standalone services you can pick and choose from, without committing to a full DI framework.

**Consider alternatives** (e.g. VContainer, Zenject) when you need scoped lifetimes, factory bindings, or constructor injection across many types. In that case, use `Installer` directly (not `MainInstaller`) for multi-interface binding within your DI composition root.

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[GameLovers GameData](https://github.com/CoderGamester/com.gamelovers.gamedata)** (v1.0.0) — automatically resolved
- **[Unity Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)** (≥ 1.21.20) — automatically resolved
- **[UniTask](https://github.com/Cysharp/UniTask)** (≥ 2.5.10) — automatically resolved

| Unity Version | Status |
|---|---|
| 6000.0+ (Unity 6) | ✅ Fully Tested |
| 2022.3 LTS | ⚠️ Untested |

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/CoderGamester/com.gamelovers.services.git`

### Via manifest.json

```json
{
  "dependencies": {
    "com.gamelovers.services": "https://github.com/CoderGamester/com.gamelovers.services.git"
  }
}
```

---

## Key Components

| Component | Responsibility |
|-----------|----------------|
| **MainInstaller** | Static service locator for global-scope single-interface bindings |
| **Installer** | Instance-based DI container (supports multi-interface binding) |
| **IMessageBrokerService** | Type-safe pub/sub messaging |
| **ITickService** | Centralized Update/FixedUpdate/LateUpdate callbacks |
| **ICoroutineService** | Run coroutines from pure C# classes |
| **IPoolService** | Object pool registry and management |
| **IDataService / IDataProvider** | Cross-platform data persistence (read-write / read-only) |
| **ITimeService / ITimeManipulator** | Unified time access with offset/sync manipulation |
| **IRngService** | Deterministic random number generation |
| **ICommandService\<TGameLogic\>** | Typed command execution layer |
| **VersionServices** | Runtime access to build/git metadata |
| **AssetResolverService** | Addressables-based typed asset loading by id + asset type |
| **IAssetLoader / ISceneLoader** | Low-level addressable load/unload/instantiate interfaces |

---

## Quick Start

```csharp
using UnityEngine;
using GameLovers.Services;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        var messageBroker = new MessageBrokerService();
        var tickService   = new TickService();
        var dataService   = new DataService();

        MainInstaller.Bind<IMessageBrokerService>(messageBroker);
        MainInstaller.Bind<ITickService>(tickService);
        MainInstaller.Bind<IDataService>(dataService);
    }

    void OnDestroy()
    {
        MainInstaller.CleanDispose<ITickService>();
        MainInstaller.Clean();
    }
}

// Resolve anywhere
var broker = MainInstaller.Resolve<IMessageBrokerService>();
broker.Subscribe<PlayerDamagedMessage>(OnPlayerDamaged);

public struct PlayerDamagedMessage : IMessage
{
    public int PlayerId;
    public float Damage;
}
```

---

## Services at a Glance

Full API reference and recipes live in [`docs/`](docs/README.md). Short examples below.

### Service Locator (MainInstaller / Installer)

```csharp
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
var broker = MainInstaller.Resolve<IMessageBrokerService>();
MainInstaller.TryResolve<IDataService>(out var ds);
MainInstaller.CleanDispose<ITickService>();
MainInstaller.Clean();

// Multi-interface binding — use Installer directly
var installer = new Installer();
installer.Bind<TimeService, ITimeService, ITimeManipulator>(new TimeService());
```

### Message Broker

```csharp
// static method subscriptions are NOT supported
broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated);
broker.Publish(new EnemyDefeatedMessage { EnemyId = 42 });
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 }); // safe during publish
broker.Unsubscribe<EnemyDefeatedMessage>(this);
broker.UnsubscribeAll(this);
```

### Tick Service

```csharp
var tick = new TickService();
tick.SubscribeOnUpdate(OnUpdate);
tick.SubscribeOnUpdate(OnThrottled, deltaTime: 0.1f); // rate-limited
tick.SubscribeOnFixedUpdate(OnFixed);
tick.SubscribeOnLateUpdate(OnLate);
tick.UnsubscribeAll(this);
tick.Dispose(); // destroys host GameObject
```

### Coroutine Service

```csharp
var cs = new CoroutineService();
IAsyncCoroutine handle = cs.StartAsyncCoroutine(MyRoutine());
handle.OnComplete(() => Debug.Log("Done!"));
cs.StartDelayCall(() => Debug.Log("2 s later"), delay: 2f);
cs.Dispose();
```

### Pool Service

```csharp
var pool = new PoolService();
pool.AddPool(new GameObjectPool<Bullet>(50, prefab));
var bullet = pool.Spawn<Bullet>();
pool.Despawn(bullet);
```

### Data Service

```csharp
var ds = new DataService();
PlayerData player = ds.LoadData<PlayerData>(); // loads from PlayerPrefs or creates fresh
player.Level = 10;
ds.SaveData<PlayerData>();
```

### RNG Service

```csharp
RngData rngData = RngService.CreateRngData(seed: 42);
var rng = new RngService(rngData);
int roll = rng.Range(1, 7);         // 1–6
int saved = rng.Counter;
rng.Restore(saved);                 // replay from saved point
```

### Time Service

```csharp
var time = new TimeService();
DateTime utc  = time.DateTimeUtcNow;
float unity   = time.UnityTimeNow;
long unixMs   = time.UnixTimeNow;
time.AddTime(3600f);                // fast-forward 1 hour (ITimeManipulator)
```

### Command Service

```csharp
public struct LevelUpCommand : IGameCommand<GameLogic>
{
    public void Execute(GameLogic gl, IMessageBrokerService mb)
    {
        gl.PlayerLevel++;
        mb.Publish(new PlayerLevelledUpMessage { Level = gl.PlayerLevel });
    }
}

ICommandService<GameLogic> cmd = new CommandService<GameLogic>(gameLogic, messageBroker);
cmd.ExecuteCommand(new LevelUpCommand());
```

### Version Services

```csharp
// Pick one of:
VersionServices.LoadVersionData();          // sync — recommended for tiny version-data.txt payloads
await VersionServices.LoadVersionDataAsync(); // async — use if VersionData embeds large blobs

string branch = VersionServices.Branch;
string commit = VersionServices.Commit;
string ext    = VersionServices.VersionExternal; // always safe, no load needed
```

### Asset Loading

```csharp
// Low-level
var loader  = new AddressablesAssetLoader();
var texture = await loader.LoadAssetAsync<Texture2D>("Textures/hero");

// High-level: typed by id
var resolver = new AssetResolverService();
resolver.AddConfigs(spriteConfigs); // AssetConfigsScriptableObject<SpriteId, Sprite>
var sprite = await resolver.RequestAsset<SpriteId, Sprite>(SpriteId.Hero, true, false);
await resolver.LoadSceneAsync<SceneId>(SceneId.MainMenu, LoadSceneMode.Single, true);
```

---

## Editor Tools

The package ships a set of editor utilities that work in both **Edit** and **Play** mode.

### Services Explorer

Open via `Tools > GameLovers > Services Explorer`.

A dockable UIToolkit window with one tab per service. During Play mode each tab live-refreshes at 250 ms intervals. In Edit mode a snapshot banner is shown and data is read on demand.

| Tab | What it shows | Primary CTA / Actions |
|---|---|---|
| **Overview** | Per-service card grid with bound/ready status and direct jump-links | Open (jumps to tab), per-service primary CTA |
| **Versioning** | `VersionExternal`, `VersionInternal`, `Branch`, `Commit`, `BuildNumber`; `version-data.txt` preview | **Reveal version-data.txt** |
| **Installer** | All `MainInstaller` bindings (interface → concrete type) | **Clean All**; Clean, CleanDispose per binding |
| **Message Broker** | All `IMessage` subscriptions with expandable subscriber lists | **Unsubscribe All**; Unsubscribe per type, Publish default(T) test |
| **Tick** | Update / FixedUpdate / LateUpdate subscriber lists with throttle settings | **Unsubscribe All**; Clear per list |
| **Coroutine** | Active `IAsyncCoroutine` handles (start time, running, completed) | **Stop All Coroutines**; Stop individual |
| **Pool** | All registered pools: spawned count, sample entity | **Clear All Pools**; DespawnAll, Dispose, RemovePool, Ping sample |
| **Data** | All loaded data types with indented JSON preview | **Save All Data**; Save, Load, Delete PlayerPrefs key |
| **Time** | Live `DateTimeUtcNow`, `UnityTimeNow`, `UnityScaleTimeNow`, `UnixTimeNow` | **Reset Time**; `AddTime` slider, `SetInitialTime` picker |
| **RNG** | Seed, Counter, peek-next N values | Restore(count) |
| **Asset Resolver** | `AssetMap` tree: asset type → id type → (id → ref, loaded status) | **Unload All** (behind destructive toggle); Unload per asset |
| **Assets Importer** | Discovered `IAssetConfigsImporter` list with per-importer path and status | **Import All**; Set Path, Import, Select per importer |
| **Addressable Ids** | Generator settings (`ScriptFilename`, `Namespace`, `AddressableLabel`) with output status | **Generate Addressable Ids**; Open Addressables Groups |

### Custom Inspectors

- **`AssetConfigsScriptableObject`** — diagnostics panel (duplicate keys, empty GUIDs) + default fields + "Regenerate Addressable Ids" button.
- **`AddressablesIdGeneratorSettings`** — settings are now configured in the Services Explorer **Addressable Ids** tab (`Tools > GameLovers > Addressable Ids > Open in Explorer`).
- **`AssetReferenceScene`** (property drawer) — resolved scene path label + "Open in Addressables Groups" button.

### Scaffolders

`Assets > Create > GameLovers Services > …`

| Entry | Generates |
|---|---|
| **Message** | `struct : IMessage` |
| **Command** | `struct : IGameCommand<TGameLogic>` |
| **Service** | `IMyService` + `MyService : IMyService, IDisposable` |
| **Pool Entity** | class implementing `IPoolEntitySpawn` + `IPoolEntityDespawn` |

File names and namespaces are set interactively in the Project window, identical to Unity's built-in "Create > C# Script" flow.

---

## Samples

Importable samples live under [`Samples~/`](Samples~/) and are exposed via the Unity Package Manager:

| Sample | Addressables required? | Focus |
|---|---|---|
| **Services Playground** | No | All foundation services (`MainInstaller`, `MessageBroker`, `Tick`, `Coroutine`, `Pool`, `Data`, `Time`, `Rng`, `Commands`, `Versioning`) wired into a single scene. Doubles as the manual end-to-end protocol for the **Services Explorer** window |
| **Asset Resolver** | Yes (~2 minutes setup) | Typed asset loading via `AssetResolverService` + `AssetConfigsScriptableObject<TId, TAsset>`, plus the Addressable Ids generator and Assets Importer pipeline |

Each sample ships as a complete, runnable Unity scene with a programmatically-built UI — no per-import wiring step (the Asset Resolver sample requires marking your sprites Addressable; see its README). For the index, AI-assistant common-mistakes section, and full list of sample-only types (which are NOT part of the package public API), see [`Samples~/README.md`](Samples~/README.md).

To import a sample: **Window > Package Manager > GameLovers Services > Samples > Import**.

---

## Contributing

Contributions are welcome! See [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.services/issues) to report bugs or request features. For development setup, architecture details, namespace conventions, and coding standards, see [AGENTS.md](AGENTS.md).

---

## Related docs

| Document | Purpose |
|---|---|
| [docs/README.md](docs/README.md) | Full per-service API reference |
| [AGENTS.md](AGENTS.md) | Contributor/agent guide (architecture, gotchas, workflows) |
| [CHANGELOG.md](CHANGELOG.md) | Version history |
| [MIGRATION.md](MIGRATION.md) | v1.x → v2.0.0 migration guide |

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.services/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.services/discussions)

## License

MIT — see [LICENSE.md](LICENSE.md).
