# GameLovers Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-2.0.0-green.svg)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-documentation) | [Contributing](#contributing)

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

### Key Features

- **🏗️ Service Locator** - Simple DI-lite pattern with `MainInstaller`
- **📨 Message Broker** - Type-safe decoupled pub/sub communication
- **⏱️ Tick Service** - Centralized Unity update cycle management
- **🔄 Coroutine Host** - Run coroutines from pure C# classes
- **🎯 Object Pooling** - Efficient GameObject and object reuse
- **💾 Data Persistence** - Cross-platform save/load with JSON serialization
- **🎲 Deterministic RNG** - Reproducible random number generation
- **📋 Version Services** - Runtime access to build/git metadata
- **🎮 Command Pattern** - Typed, decoupled command execution layer
- **⏰ Time Service** - Unified access to Unity/Unix/DateTime with time manipulation

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[GameLovers GameData](https://github.com/CoderGamester/com.gamelovers.gamedata)** (v1.0.0) - Automatically resolved
- **[Unity Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)** (≥ 1.21.20) - Automatically resolved
- **[UniTask](https://github.com/Cysharp/UniTask)** (≥ 2.5.10) - Automatically resolved

### Compatibility Matrix

| Unity Version | Status | Notes |
|---------------|--------|-------|
| 6000.0+ (Unity 6) | ✅ Fully Tested | Primary development target |
| 2022.3 LTS | ⚠️ Untested | May require minor adaptations |

| Platform | Status | Notes |
|----------|--------|-------|
| Standalone (Windows/Mac/Linux) | ✅ Tested | Primary development and test target |
| WebGL | ⚠️ Expected to work | No platform-specific tests |
| Mobile (iOS/Android) | ⚠️ Expected to work | No platform-specific tests |
| Console | ⚠️ Untested | No platform-specific tests |

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click the `+` button and select `Add package from git URL`
3. Enter the following URL:
   ```
   https://github.com/CoderGamester/com.gamelovers.services.git
   ```

### Via manifest.json

Add the following line to your project's `Packages/manifest.json`:

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
| **IMessageBrokerService** | Type-safe pub/sub messaging interface |
| **ITickService** | Centralized Update/FixedUpdate/LateUpdate callbacks |
| **ICoroutineService** | Run coroutines without MonoBehaviour |
| **IPoolService** | Object pool registry and management |
| **IDataService** | Cross-platform data persistence |
| **IDataProvider** | Read-only data access (subset of `IDataService`) |
| **ITimeService** | Unified time access (Unity/Unix/DateTime) |
| **ITimeManipulator** | Extends `ITimeService` with time offset/sync manipulation |
| **IRngService** | Deterministic random number generation |
| **ICommandService\<TGameLogic\>** | Typed command execution layer |
| **VersionServices** | Runtime build/git metadata |
| **AssetResolverService** | Addressables-based typed asset loading by id + asset type |
| **IAssetLoader** | Low-level addressable load/unload/instantiate interface |
| **ISceneLoader** | Low-level addressable scene load/unload interface |

---

## Quick Start

### 1. Initialize Services

```csharp
using UnityEngine;
using GameLovers.Services;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Create service instances
        var messageBroker = new MessageBrokerService();
        var tickService = new TickService();
        var dataService = new DataService();
        
        // Bind to MainInstaller (interfaces only)
        MainInstaller.Bind<IMessageBrokerService>(messageBroker);
        MainInstaller.Bind<ITickService>(tickService);
        MainInstaller.Bind<IDataService>(dataService);
    }

    void OnDestroy()
    {
        // Clean up on shutdown
        MainInstaller.CleanDispose<ITickService>();   // Dispose + remove
        MainInstaller.Clean();                        // Remove remaining bindings
    }
}
```

### 2. Use Services Anywhere

```csharp
using GameLovers.Services;

public class PlayerController
{
    public PlayerController()
    {
        // Resolve services
        var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();
        
        // Subscribe to events
        messageBroker.Subscribe<PlayerDamagedMessage>(OnPlayerDamaged);
    }

    private void OnPlayerDamaged(PlayerDamagedMessage message) { }
}

// Define messages as structs implementing IMessage
public struct PlayerDamagedMessage : IMessage
{
    public int PlayerId;
    public float Damage;
}
```

---

## Services Documentation

### Main Installer

Lightweight service locator for managing dependencies globally.

**Key Points:**
- Only **interfaces** can be bound (throws if you try to bind a concrete type)
- Binding is **instance-based** — you provide the instance, not the type
- `MainInstaller` is a static class; use `Installer` directly for **multi-interface binding**

```csharp
// Bind a single interface
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
MainInstaller.Bind<IDataService>(new DataService());

// Resolve
var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();

// Safe resolve (returns false instead of throwing)
if (MainInstaller.TryResolve<IDataService>(out var dataService))
{
    dataService.SaveAllData();
}

// Clean up
MainInstaller.Clean<IMessageBrokerService>();  // Remove single binding
MainInstaller.CleanDispose<ITickService>();     // Dispose + remove
MainInstaller.Clean();                          // Clear all bindings

// Multi-interface binding requires using Installer directly
var installer = new Installer();
var timeService = new TimeService();
installer.Bind<TimeService, ITimeService, ITimeManipulator>(timeService);

// Bind calls are chainable
installer.Bind<IMessageBrokerService>(new MessageBrokerService())
         .Bind<ITickService>(new TickService());
```

---

### Message Broker Service

Decoupled pub/sub communication between game systems.

**Key Points:**
- Static method subscriptions are **not supported** (subscriber is keyed by `action.Target`)
- `Publish` throws if `Subscribe`/`Unsubscribe` is called during publish — use `PublishSafe` in that case
- `Unsubscribe<T>(null)` removes **all** subscribers for that message type
- `UnsubscribeAll(null)` clears **everything** from the broker

```csharp
public struct EnemyDefeatedMessage : IMessage
{
    public int EnemyId;
    public Vector3 Position;
}

var broker = new MessageBrokerService();

// Subscribe (instance methods only — static methods are not supported)
broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated);

// Publish
broker.Publish(new EnemyDefeatedMessage { EnemyId = 42, Position = Vector3.zero });

// Use PublishSafe when handlers may subscribe/unsubscribe during publish
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 });

// Unsubscribe this object from one message type
broker.Unsubscribe<EnemyDefeatedMessage>(this);

// Unsubscribe ALL subscribers from one message type
broker.Unsubscribe<EnemyDefeatedMessage>();

// Unsubscribe this object from all message types
broker.UnsubscribeAll(this);

// Clear the entire broker
broker.UnsubscribeAll();
```

---

### Tick Service

Centralized control over Unity's update cycle.

**Key Points:**
- Creates a `DontDestroyOnLoad` GameObject to drive callbacks
- Subscribe with an `Action<float>` (receives elapsed `deltaTime`)
- `SubscribeOnUpdate` supports an optional `deltaTime` buffer for rate-limited ticking
- Call `Dispose()` to tear down the host GameObject (tests, game reset)

```csharp
public class GameController : IDisposable
{
    private readonly ITickService _tickService;

    public GameController()
    {
        _tickService = new TickService();

        // Subscribe to every frame Update
        _tickService.SubscribeOnUpdate(OnUpdate);

        // Subscribe to Update, throttled to run at most every 0.1 seconds
        _tickService.SubscribeOnUpdate(OnUpdateBuffered, deltaTime: 0.1f);

        // Subscribe to FixedUpdate
        _tickService.SubscribeOnFixedUpdate(OnFixedUpdate);

        // Subscribe to LateUpdate
        _tickService.SubscribeOnLateUpdate(OnLateUpdate);
    }

    private void OnUpdate(float deltaTime) { /* called every frame */ }
    private void OnUpdateBuffered(float deltaTime) { /* called every ~0.1s */ }
    private void OnFixedUpdate(float deltaTime) { /* called on FixedUpdate */ }
    private void OnLateUpdate(float deltaTime) { /* called on LateUpdate */ }

    public void Dispose()
    {
        // Remove a single callback by reference
        _tickService.Unsubscribe(OnUpdate);

        // Or remove all subscriptions from this object at once
        _tickService.UnsubscribeAll(this);
        _tickService.Dispose();  // Destroys the host GameObject
    }
}
```

---

### Coroutine Service

Run Unity coroutines from pure C# classes without MonoBehaviour.

**Key Points:**
- `StartCoroutine` returns a plain Unity `Coroutine` handle (no callbacks)
- `StartAsyncCoroutine` returns `IAsyncCoroutine` with `OnComplete` callback and state flags
- `StartDelayCall(action, delay)` — argument order: action first, delay second

```csharp
var coroutineService = new CoroutineService();

// Plain coroutine — returns Unity Coroutine handle
Coroutine handle = coroutineService.StartCoroutine(MyRoutine());
coroutineService.StopCoroutine(handle);

// Async coroutine — returns IAsyncCoroutine with callback and state
IAsyncCoroutine asyncHandle = coroutineService.StartAsyncCoroutine(MyRoutine());
asyncHandle.OnComplete(() => Debug.Log("Done!"));

if (asyncHandle.IsRunning) { /* still running */ }
if (asyncHandle.IsCompleted) { /* finished naturally */ }

// Stop the coroutine (note: triggerOnComplete flag is currently not respected — callbacks always fire)
asyncHandle.StopCoroutine(triggerOnComplete: false);

// Async coroutine with typed result data
IAsyncCoroutine<int> typedHandle = coroutineService.StartAsyncCoroutine(MyRoutine(), data: 42);
typedHandle.OnComplete(result => Debug.Log($"Finished with: {result}"));

// Delayed call — action fires after delay seconds
coroutineService.StartDelayCall(() => Debug.Log("2 seconds later"), delay: 2f);

// Delayed call with typed data
coroutineService.StartDelayCall<string>(msg => Debug.Log(msg), data: "Hello", delay: 1f);

// Stop all coroutines at once
coroutineService.StopAllCoroutines();

// Tear down the host GameObject when done (tests, game reset)
coroutineService.Dispose();

IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("Step");
}
```

---

### Pool Service

Efficient object pooling with lifecycle hooks.

```csharp
var poolService = new PoolService();

// Create and register a pool
var bulletPool = new GameObjectPool<Bullet>(initSize: 50, bulletPrefab);
poolService.AddPool(bulletPool);

// Spawn / Despawn
var bullet = poolService.Spawn<Bullet>();
poolService.Despawn(bullet);

// Spawn with data (entity must implement IPoolEntitySpawn<BulletData>)
var bullet = poolService.Spawn<Bullet, BulletData>(new BulletData { Damage = 100 });

// Get direct pool access
IObjectPool<Bullet> pool = poolService.GetPool<Bullet>();
pool.DespawnAll();

// Despawn all via service
poolService.DespawnAll<Bullet>();

// Remove a pool without destroying its entities
poolService.RemovePool<Bullet>();

// Dispose a pool and optionally destroy its sample entity
poolService.Dispose<Bullet>(disposeSampleEntity: true);
```

**Key Points:**
- `GameObjectPool<T>` requires `T : Behaviour` — use it when you want a typed component reference on spawn
- `GameObjectPool` (non-generic) works with raw `GameObject` references
- `PoolService` keeps one pool per type; calling `AddPool<T>()` for an already-registered type throws

**Lifecycle Hooks (implement on your entity):**

| Interface | When Called |
|-----------|-------------|
| `IPoolEntitySpawn` | On every spawn (no data) |
| `IPoolEntitySpawn<TData>` | On spawn with typed data |
| `IPoolEntityDespawn` | On despawn |
| `IPoolEntityObject<T>` | On first creation — receives pool reference for self-despawn |

---

### Data Service

Cross-platform persistent data storage with JSON serialization.

**Key Points:**
- Data is keyed by **type** (`typeof(T)`) — no string keys
- Only **reference types** (`class`) are supported
- `GetData<T>()` throws `KeyNotFoundException` if the type has not been loaded or added — use `HasData<T>()` to guard
- `LoadData<T>` requires `T` to have a **parameterless constructor** (creates a fresh instance if no saved data exists)
- Keys stored in `PlayerPrefs` use `typeof(T).Name` — watch for name collisions across assemblies

```csharp
[Serializable]
public class PlayerData
{
    public string Name;
    public int Level;
    public PlayerData() { }  // required for LoadData<T> when no saved data exists
}

var dataService = new DataService();

// Load from disk (or create fresh if not saved yet)
PlayerData player = dataService.LoadData<PlayerData>();

// Modify in memory
player.Name = "Hero";
player.Level = 10;

// Save one type to disk
dataService.SaveData<PlayerData>();

// Save all types to disk
dataService.SaveAllData();

// Add or replace in memory without saving to disk
dataService.AddOrReplaceData(new PlayerData { Name = "Alt", Level = 5 });

// Read back from memory
PlayerData loaded = dataService.GetData<PlayerData>();

// Check if data exists in memory
bool exists = dataService.HasData<PlayerData>();
```

---

### RNG Service

Deterministic random number generation with state management.

**Key Points:**
- State can be saved/restored for replay or rollback
- Uses `floatP` from `com.gamelovers.gamedata` for deterministic float math
- `Peek`/`PeekRange` return the next value without advancing state

```csharp
// Create with seed
RngData rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

// Generate values
int randomInt      = rng.Next;                          // 0 to int.MaxValue
floatP randomFloat = rng.Nextfloat;                     // 0 to floatP.MaxValue
int ranged         = rng.Range(1, 100);                 // 1–99 (max exclusive by default)
floatP rangedFloat = rng.Range((floatP)0f, (floatP)1f); // 0–1 (max inclusive by default)

// Peek without advancing state
int peeked      = rng.Peek;               // same value on repeated calls
int peekedRange = rng.PeekRange(1, 100);

// Save and restore state for determinism / rollback
int savedCount = rng.Counter;
// ... generate some values ...
rng.Restore(savedCount);  // rewind to saved state
```

---

### Version Services

Runtime access to build version and git metadata.

**Key Points:**
- Requires `version-data.txt` in Resources (generated by Editor tools on project load and before builds)
- Call `LoadVersionDataAsync()` once at startup — **all properties except `VersionExternal` throw** if called before data is loaded

```csharp
using GameLovers.Services;

// Call once at startup (e.g. in a boot sequence)
await VersionServices.LoadVersionDataAsync();

// Safe at any time — reads Application.version directly
string externalVersion = VersionServices.VersionExternal;  // "1.0.1"

// These require LoadVersionDataAsync() to have completed first
string internalVersion = VersionServices.VersionInternal;   // "1.0.1-42.main.abc123"
string branch          = VersionServices.Branch;            // "main"
string commit          = VersionServices.Commit;            // "abc123"
string buildNumber     = VersionServices.BuildNumber;       // "42"

// Check if app is outdated against a remote version string
bool outdated = VersionServices.IsOutdatedVersion("1.2.0");
```

---

### Time Service

Unified time access with manipulation support.

**Key Points:**
- Bind as `ITimeManipulator` if you need to manipulate time; bind as `ITimeService` for read-only consumers
- All time getters account for any offset applied via `AddTime`

```csharp
var timeService = new TimeService();

// Query current times
DateTime utcNow  = timeService.DateTimeUtcNow;    // current UTC datetime
float unityTime  = timeService.UnityTimeNow;      // Time.realtimeSinceStartup + offset
float scaledTime = timeService.UnityScaleTimeNow; // Time.time + offset
long unixMs      = timeService.UnixTimeNow;       // Unix time in milliseconds

// Conversions
long unix    = timeService.UnixTimeFromDateTimeUtc(DateTime.UtcNow);
DateTime dt  = timeService.DateTimeUtcFromUnixTime(unix);
float unityT = timeService.UnityTimeFromUnixTime(unix);

// Manipulation (ITimeManipulator only)
timeService.AddTime(3600f);                     // fast-forward 1 hour
timeService.SetInitialTime(DateTime.UtcNow);    // sync with server time
```

---

### Command Service

Typed, decoupled command execution with message broker integration.

**Key Points:**
- Commands implement `IGameCommand<TGameLogic>` and are executed synchronously
- Use structs for simple fire-and-forget commands; use classes when you need reference semantics
- `CommandService<TGameLogic>` exposes `protected GameLogic` and `protected MessageBroker` for subclassing

```csharp
// Define your game logic container
public class GameLogic
{
    public int PlayerLevel;
}

// Define a command
public struct LevelUpCommand : IGameCommand<GameLogic>
{
    public void Execute(GameLogic gameLogic, IMessageBrokerService messageBroker)
    {
        gameLogic.PlayerLevel++;
        messageBroker.Publish(new PlayerLevelledUpMessage { Level = gameLogic.PlayerLevel });
    }
}

// Set up
var gameLogic = new GameLogic();
var messageBroker = new MessageBrokerService();
ICommandService<GameLogic> commandService = new CommandService<GameLogic>(gameLogic, messageBroker);

// Execute
commandService.ExecuteCommand(new LevelUpCommand());

// Extend CommandService to add cross-cutting behaviour
public class MyCommandService : CommandService<GameLogic>
{
    public MyCommandService(GameLogic logic, IMessageBrokerService broker) : base(logic, broker) { }

    public void CustomOperation()
    {
        // Access protected base properties
        GameLogic.PlayerLevel++;
        MessageBroker.Publish(new SomeMessage());
    }
}
```

---

### Asset Loading & Import

Addressables-based asset loading, typed by id and asset type, with optional editor-time import pipeline.

**Key Points:**
- `AssetResolverService` is in namespace `GameLovers.Services`; the loader interfaces (`IAssetLoader`, `ISceneLoader`) and support types are in `GameLovers.Services.AssetsImporter`
- Assets must be registered via `AddConfigs`, `AddAssets`, or `AddAsset` before calling `RequestAsset` or `LoadSceneAsync<TId>`
- `AddressablesAssetLoader` implements both `IAssetLoader` and `ISceneLoader` and is the low-level entry point for raw addressable keys
- Editor tools (Tools → Assets Importer / Tools → AddressableIds Generator) live in `Editor/AssetsImporter/`

```csharp
using GameLovers.Services;
using GameLovers.Services.AssetsImporter;

// Low-level: load any asset by key directly
var loader = new AddressablesAssetLoader();
var texture = await loader.LoadAssetAsync<Texture2D>("Textures/hero_avatar");

// High-level: register configs and request by typed id
public class AssetBootstrap
{
    public async void Init(
        SpritesScriptableObject spriteConfigs,   // AssetConfigsScriptableObject<SpriteId, Sprite>
        ScenesScriptableObject  sceneConfigs)    // AssetConfigsScriptableObject<SceneId, Scene>
    {
        var assetResolver = new AssetResolverService();
        
        // Register weak-link configs from ScriptableObjects
        assetResolver.AddConfigs(spriteConfigs);
        assetResolver.AddConfigs(sceneConfigs);
        
        // Load and instantiate a sprite by typed id
        var sprite = await assetResolver.RequestAsset<SpriteId, Sprite>(
            SpriteId.HeroAvatar, loadAsynchronously: true, instantiate: false);
        
        // Load a scene
        await assetResolver.LoadSceneAsync<SceneId>(
            SceneId.MainMenu, LoadSceneMode.Single, activateOnLoad: true);
        
        // Bulk-load all registered sprites at once
        var allSprites = await assetResolver.LoadAllAssets<SpriteId, Sprite>();
    }
}

// Define a ScriptableObject config container (one per asset type)
[CreateAssetMenu(fileName = "SpritesConfig", menuName = "Configs/SpritesConfig")]
public class SpritesScriptableObject : AssetConfigsScriptableObject<SpriteId, Sprite> { }
```

**Asset Import Pipeline (Editor):**

```csharp
// Implement a custom importer for a new asset type
public class EnemySpritesImporter : AssetsConfigsImporter<EnemyId, Sprite, EnemySpriteConfig>
{
    // Ids are inferred from the enum values — override IdPattern to customise name matching
}

// Open Tools → Assets Importer to run all importers and populate ScriptableObject configs
```

---

## Contributing

We welcome contributions! Here's how you can help:

### Reporting Issues

- Use the [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.services/issues) page
- Include Unity version, package version, and reproduction steps
- Attach relevant code samples, error logs, or screenshots

### Development Setup

1. Fork the repository on GitHub
2. Clone your fork: `git clone https://github.com/yourusername/com.gamelovers.services.git`
3. Create a feature branch: `git checkout -b feature/amazing-feature`
4. Make your changes with tests
5. Commit: `git commit -m 'Add amazing feature'`
6. Push: `git push origin feature/amazing-feature`
7. Create a Pull Request

### Code Guidelines

- Follow C# 9.0 syntax with explicit namespaces (no global usings)
- Add XML documentation to all public APIs
- Include unit tests for new features
- Runtime code must not reference `UnityEditor`
- Update CHANGELOG.md for notable changes

---

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.services/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.services/discussions)
- **Changelog**: See [CHANGELOG.md](CHANGELOG.md) for version history

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

---

**Made with ❤️ for the Unity community**

*If this package helps your project, please consider giving it a ⭐ on GitHub!*
