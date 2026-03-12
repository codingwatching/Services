# GameLovers.Services - AI Agent Guide

## 1. Package Overview
- **Package**: `com.gamelovers.services`
- **Unity**: 6000.0+
- **Dependencies** (see `package.json`)
  - `com.gamelovers.gamedata` (**1.0.0**) — provides `floatP`, used by `RngService`

This package provides a set of small, modular "foundation services" for Unity projects (service locator/DI-lite, messaging, ticking, coroutines, pooling, persistence, RNG, time, command pattern, and build version helpers).

For user-facing docs, treat `README.md` as the primary entry point. This file is for contributors/agents working on the package itself.

## 2. Runtime Architecture (high level)

### Interface-to-Concrete Lookup

| Interface | Implementation | File |
|-----------|---------------|------|
| `IInstaller` | `Installer` | `Runtime/Installer.cs` |
| `IMessageBrokerService` | `MessageBrokerService` | `Runtime/MessageBrokerService.cs` |
| `ITickService` | `TickService` | `Runtime/TickService.cs` |
| `ICoroutineService` | `CoroutineService` | `Runtime/CoroutineService.cs` |
| `IPoolService` | `PoolService` | `Runtime/PoolService.cs` |
| `IObjectPool<T>` | `ObjectPool<T>`, `GameObjectPool`, `GameObjectPool<T>` | `Runtime/ObjectPool.cs` |
| `IDataProvider` / `IDataService` | `DataService` | `Runtime/DataService.cs` |
| `ITimeService` / `ITimeManipulator` | `TimeService` | `Runtime/TimeService.cs` |
| `IRngService` | `RngService` | `Runtime/RngService.cs` |
| `ICommandService<TGameLogic>` | `CommandService<TGameLogic>` | `Runtime/CommandService.cs` |


### Service Locator / Bindings
`Runtime/Installer.cs`, `Runtime/MainInstaller.cs`
- `Installer` stores a `Dictionary<Type, object>` of interface type → instance.
- `MainInstaller` is a **static** wrapper over a single `Installer` instance (global scope).
- Binding is **instance-based** (`Bind<T>(T instance)`), not "type-to-type" or lifetime-managed DI.
- Only **interfaces** can be bound (binding a non-interface throws `ArgumentException`).
- `Installer.Bind<T, T1, T2>(instance)` binds one instance to two interfaces simultaneously. `Installer.Bind<T, T1, T2, T3>(instance)` also exists for three interfaces. `MainInstaller` only exposes single-interface `Bind<T>`.
- `Bind` calls are chainable (returns `IInstaller`).
- Re-binding the same interface throws (`Dictionary.Add` — no overwrite semantics).

### Messaging
`Runtime/MessageBrokerService.cs`
- Message contract: `IMessage`
- Pub/sub via `IMessageBrokerService`:
  - `Publish<T>(T message)` — iterates subscribers directly; throws if `Subscribe`/`Unsubscribe` is called during publish.
  - `PublishSafe<T>(T message)` — copies delegate list first; safe for chain subscribe/unsubscribe during publish (allocates).
  - `Subscribe<T>(Action<T> action)` — keyed by `action.Target`; **static methods throw**.
  - `Unsubscribe<T>(object subscriber = null)` — `null` removes **all** subscribers for that type.
  - `UnsubscribeAll(object subscriber = null)` — `null` clears **everything**.

### Tick / Update Fan-Out
`Runtime/TickService.cs`
- Creates a `DontDestroyOnLoad` GameObject with `TickServiceMonoBehaviour` to drive Unity callbacks.
- Subscriber API (all take `Action<float> action`):
  - `SubscribeOnUpdate(action, deltaTime=0f, timeOverflowToNextTick=false, realTime=false)`
  - `SubscribeOnLateUpdate(action, deltaTime=0f, timeOverflowToNextTick=false, realTime=false)`
  - `SubscribeOnFixedUpdate(action)`
  - `Unsubscribe(action)` — removes from all lists
  - `UnsubscribeOnUpdate/OnFixedUpdate/OnLateUpdate(action)` — type-specific single removal
  - `UnsubscribeAllOnUpdate()` / `UnsubscribeAllOnUpdate(object)` — bulk clear Update list (all or by subscriber)
  - `UnsubscribeAllOnFixedUpdate()` / `UnsubscribeAllOnFixedUpdate(object)` — bulk clear FixedUpdate list
  - `UnsubscribeAllOnLateUpdate()` / `UnsubscribeAllOnLateUpdate(object)` — bulk clear LateUpdate list
  - `UnsubscribeAll()` / `UnsubscribeAll(object subscriber)` — clears all lists (all or by subscriber)
- `deltaTime > 0` enables buffered ticking (rate-limited). `timeOverflowToNextTick` carries overflow to reduce drift.
- `realTime=true` uses `Time.realtimeSinceStartup`; `false` (default) uses `Time.time`.

### Coroutine Host
`Runtime/CoroutineService.cs`
- Creates a `DontDestroyOnLoad` GameObject with `CoroutineServiceMonoBehaviour`.
- API:
  - `StartCoroutine(IEnumerator)` → `Coroutine` (plain Unity handle, no callbacks)
  - `StartAsyncCoroutine(IEnumerator)` → `IAsyncCoroutine` (has `OnComplete(Action)`, `IsRunning`, `IsCompleted`, `StopCoroutine(bool)`)
  - `StartAsyncCoroutine<T>(IEnumerator, T data)` → `IAsyncCoroutine<T>` (adds `Data` and `OnComplete(Action<T>)`)
  - `StartDelayCall(Action call, float delay)` → `IAsyncCoroutine` — argument order: action first, delay second
  - `StartDelayCall<T>(Action<T> call, T data, float delay)` → `IAsyncCoroutine<T>`
  - `StopCoroutine(Coroutine)`, `StopAllCoroutines()`

### Pooling
`Runtime/PoolService.cs`, `Runtime/ObjectPool.cs`
- Pool registry: `PoolService : IPoolService` — one pool per type.
- Pool implementations:
  - `ObjectPool<T>` — generic; lifecycle hooks via direct cast (`IPoolEntitySpawn`, `IPoolEntityDespawn`)
  - `GameObjectPool` — `GameObject` pools; lifecycle hooks via `GetComponent<>()`; manages `SetActive`
  - `GameObjectPool<T> where T : Behaviour` — component-typed; same `GetComponent<>()` hook pattern
- `IObjectPool<T>` surface: `Spawn()`, `Spawn<TData>(data)`, `Despawn(entity)`, `Despawn(bool onlyFirst, Func<T,bool>)`, `DespawnAll()`, `Reset(uint, T)`, `Clear()`, `SampleEntity`, `SpawnedReadOnly`
- Lifecycle hook interfaces: `IPoolEntitySpawn`, `IPoolEntitySpawn<T>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`
- `CallOnSpawned`/`CallOnDespawned` are **virtual** in `ObjectPoolBase<T>` — override to customize lifecycle dispatch.

### Persistence
`Runtime/DataService.cs`
- `IDataProvider` — read-only interface: `GetData<T>()`, `HasData<T>()`
- `IDataService : IDataProvider` — full interface: adds `AddOrReplaceData<T>(T)`, `LoadData<T>()`, `SaveData<T>()`, `SaveAllData()`
- In-memory store keyed by `Type` (not string). Only **reference types** (`where T : class`) supported.
- Disk persistence via `PlayerPrefs` + `Newtonsoft.Json` serialization. Key = `typeof(T).Name`.

### Time + Manipulation
`Runtime/TimeService.cs`
- `ITimeService` — read-only: `DateTimeUtcNow`, `UnityTimeNow`, `UnityScaleTimeNow`, `UnixTimeNow`, plus conversion methods.
- `ITimeManipulator : ITimeService` — adds `AddTime(float)`, `SetInitialTime(DateTime)`.
- `TimeService` implements `ITimeManipulator`. Bind as `ITimeManipulator` for write access; `ITimeService` for read-only consumers.

### Deterministic RNG
`Runtime/RngService.cs`
- `RngData` / `IRngData` — state container (Seed, Count, State array).
- `IRngService` API: `Next`, `Nextfloat`, `Peek`, `Peekfloat`, `PeekRange(...)`, `Range(...)`, `Restore(int count)`, `Counter`, `Data`
- `RngService.CreateRngData(int seed)` — static factory for `RngData`.
- Float API uses `floatP` from `com.gamelovers.gamedata`.

### Command Pattern
`Runtime/CommandService.cs`
- Command contract: `IGameCommand<TGameLogic>` with `void Execute(TGameLogic, IMessageBrokerService)`.
- Server-only variant: `IGameServerCommand<TGameLogic>` with `void ExecuteLogic(TGameLogic)`.
- Service: `ICommandService<TGameLogic>` → `CommandService<TGameLogic>(TGameLogic, IMessageBrokerService)`.
- `CommandService` exposes `protected TGameLogic GameLogic` and `protected IMessageBrokerService MessageBroker` for subclassing (added in v0.15.1).
- Execution is **synchronous**. Use struct commands for fire-and-forget; class commands for reference semantics.

### Build/Version Info
`Runtime/VersionServices.cs`
- Static class. Requires `version-data` TextAsset in Resources.
- `LoadVersionDataAsync()` — async; call once at startup.
- `VersionExternal` — safe at any time (reads `Application.version`).
- `VersionInternal`, `Branch`, `Commit`, `BuildNumber` — **throw** if called before `LoadVersionDataAsync()` completes.

## 3. Key Directories / Files

- **Runtime**: `Runtime/`
  - Entry points: `MainInstaller.cs`, `Installer.cs`
  - Services: `MessageBrokerService.cs`, `TickService.cs`, `CoroutineService.cs`, `PoolService.cs`, `DataService.cs`, `TimeService.cs`, `RngService.cs`, `VersionServices.cs`, `CommandService.cs`
  - Pooling: `ObjectPool.cs`
- **Editor**: `Editor/`
  - Version data generation: `VersionEditorUtils.cs`, `GitEditorProcess.cs`
  - Must remain editor-only (relies on `UnityEditor` + starting git processes)
- **Tests**: `Tests/`
  - `EditMode/Unit/` — NUnit + NSubstitute; tests all non-MonoBehaviour services
  - `EditMode/Performance/` — `Unity.PerformanceTesting`; ObjectPool, MessageBroker perf
  - `PlayMode/Unit/` — TickService, CoroutineService, GameObjectPool (require a runtime)
  - `PlayMode/Integration/` — `ServiceLifecycleTest` full bootstrap/teardown
  - `PlayMode/Performance/` — TickService, GameObjectPool perf
  - `PlayMode/Smoke/` — `ServicesBootstrapSmokeTest`

## 3.5. Test Coverage Gaps

Known untested public API surface (as of v1.0.1). Add tests here when modifying these areas:

- **CoroutineService**: `StartDelayCall`, `StartDelayCall<T>`, `Dispose()`
- **TickService**: `SubscribeOnFixedUpdate`, `SubscribeOnLateUpdate`, `UnsubscribeOnFixedUpdate`, `UnsubscribeOnLateUpdate`, `UnsubscribeAllOnUpdate/FixedUpdate/LateUpdate` (and subscriber-scoped overloads), `UnsubscribeAll(object subscriber)` (targeted)
- **ObjectPool\<T\>**: `Despawn(bool onlyFirst, Func<T,bool>)`, `IsSpawned`, `Reset`, `SpawnedReadOnly`, `SampleEntity`; `IPoolEntityObject<T>.Init`/`Despawn` (test was commented out)
- **GameObjectPool\<T\>** (component-typed): entirely untested
- **PoolService**: `Clear()`, `Dispose<T>(bool)`, `Spawn<T,TData>(data)` via service
- **Installer**: `Bind<T,T1,T2>`, `Bind<T,T1,T2,T3>` (multi-interface overloads)
- **RngService**: `Nextfloat`, `Peekfloat`, `Range(floatP,floatP)`, `PeekRange(floatP,floatP)`
- **DataService**: `SaveAllData()`
- **VersionServices**: `LoadVersionDataAsync()`, `VersionInternal`, `Branch`, `Commit`, `BuildNumber`

## 4. Important Behaviors / Gotchas

### MainInstaller API
- `MainInstaller` exposes only single-interface `Bind<T>`. Multi-interface `Bind<T, T1, T2>` is on `IInstaller`/`Installer` directly.
- There is no `MainInstaller.Instance` — it is a static class wrapping a private `Installer`.

### Message Broker Mutation Safety
- `Publish<T>` iterates subscribers directly; calling `Subscribe`/`Unsubscribe` during publish **throws**.
- Use `PublishSafe<T>` if handlers may subscribe/unsubscribe during message handling (copies delegates first, at allocation cost).
- `Subscribe` uses `action.Target` as key — **static method subscriptions throw `ArgumentException`**.

### Tick / Coroutine Host GameObjects
- `TickService` and `CoroutineService` each create a `DontDestroyOnLoad` GameObject. Call `Dispose()` to tear them down (tests, game reset, domain reload).
- These services do **not** enforce a singleton; constructing multiple instances creates multiple host GameObjects.

### IAsyncCoroutine.StopCoroutine(triggerOnComplete)
- The current implementation always triggers completion callbacks regardless of the `triggerOnComplete` flag (parameter is not respected). Do not rely on cancellation-without-callback semantics.

### DataService Persistence
- Keys in `PlayerPrefs` are `typeof(T).Name` — name collisions are possible across assemblies with types sharing the same name.
- `LoadData<T>` uses `Activator.CreateInstance<T>()` when no saved data exists; `T` must have a **parameterless constructor**.
- Only reference types (`class`) are supported; value types (`struct`) are not.

### Pool Lifecycle
- `PoolService` keeps **one pool per type**; `AddPool<T>()` will throw (`Dictionary.Add`) if the type is already registered.
- `GameObjectPool.Dispose(bool disposeSampleEntity)` destroys the `SampleEntity` GameObject when `true`. `GameObjectPool.Dispose()` destroys all pooled instances but not the sample reference.
- `GameObjectPool` / `GameObjectPool<T>` use `GetComponent<>()` for lifecycle hooks on components. `ObjectPool<T>` casts the entity directly. This determines where `IPoolEntitySpawn` etc. must be implemented.

### CommandService Inheritance
- `CommandService<TGameLogic>` has `protected TGameLogic GameLogic` and `protected IMessageBrokerService MessageBroker` accessible in subclasses.
- `ExecuteCommand` is not declared `virtual`; to intercept execution, subclass and shadow with `new`, or implement `ICommandService<TGameLogic>` directly.

### Version Data Pipeline
- Runtime expects a Resources TextAsset named `version-data` (`VersionServices.VersionDataFilename`).
- `VersionEditorUtils` writes `Assets/Configs/Resources/version-data.txt` on editor load and can be invoked before builds. It uses git CLI; failures should be handled gracefully.
- `VersionExternal` is always safe (reads `Application.version` directly). All other `VersionServices` accessors throw if data has not been loaded — call `LoadVersionDataAsync()` early in boot.

### Error Quick-Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `Installer.Bind<T>(instance)` | `ArgumentException` | `T` is not an interface |
| `Installer.Bind<T>(instance)` (duplicate) | `ArgumentException` | `T` already bound |
| `MainInstaller.Resolve<T>()` | `KeyNotFoundException` | `T` not bound |
| `broker.Subscribe(staticMethod)` | `ArgumentException` | `action.Target` is null |
| `broker.Publish<T>()` | Exception | `Subscribe`/`Unsubscribe` called during iteration |
| `dataService.GetData<T>()` | `KeyNotFoundException` | `T` not loaded or added |
| `dataService.LoadData<T>()` | `MissingMethodException` | `T` has no parameterless constructor |
| `poolService.AddPool<T>(pool)` (duplicate) | `ArgumentException` | Pool for `T` already registered |
| `VersionServices.*` (except `VersionExternal`) | `NullReferenceException` | `LoadVersionDataAsync()` not called |

## 5. Coding Standards (Unity 6 / C# 9.0)
- **C#**: C# 9.0 syntax; explicit namespaces; no global usings.
- **Assemblies**
  - Runtime must not reference `UnityEditor`.
  - Editor tooling must live under `Editor/` (or be guarded with `#if UNITY_EDITOR` if absolutely necessary).
- **Performance**
  - Be mindful of allocations in hot paths (e.g., `PublishSafe` allocates; tick lists mutate; avoid per-frame allocations).

## 6. External Package Sources (for API lookups)
Prefer local UPM cache / local packages when needed:
- GameData (`floatP`, `MathfloatP`): `Packages/com.gamelovers.gamedata/`
- Unity Newtonsoft JSON: check `Library/PackageCache/` if you need source details

## 7. Dev Workflows (common changes)

### Add a new service
- Add runtime interface + implementation under `Runtime/` (keep UnityEngine usage minimal if possible).
- Add/adjust tests under `Tests/`.
- If the service needs Unity callbacks, follow the `TickService`/`CoroutineService` pattern (single `DontDestroyOnLoad` host object + `Dispose()`).

### Bind/resolve services
- Bind instances via `MainInstaller.Bind<IMyService>(myServiceInstance)`.
- Resolve via `MainInstaller.Resolve<IMyService>()` or `TryResolve`.
- Clear bindings on reset via `MainInstaller.Clean()` (or `Clean<T>()` / `CleanDispose<T>()`).
- For multi-interface binding, use an `Installer` instance directly.

### Add a new command
- Define a struct (sync, fire-and-forget) or class implementing `IGameCommand<TGameLogic>`.
- Implement `void Execute(TGameLogic gameLogic, IMessageBrokerService messageBroker)`.
- Add unit tests under `Tests/EditMode/Unit/CommandServiceTest.cs`.

### Update versioning
- Ensure `version-data.txt` exists/updates correctly in `Assets/Configs/Resources/`.
- If changing `VersionServices.VersionData`, update both runtime parsing and `VersionEditorUtils` writing logic.

## 8. Update Policy
Update this file when:
- The binding/service-locator API changes (`Installer`, `MainInstaller`)
- Core service behavior changes (publish safety rules, tick timing, coroutine completion/cancellation semantics, pooling lifecycle, command execution)
- Versioning pipeline changes (resource filename, editor generator behavior, runtime parsing)
- Dependencies change (`package.json`, new external types like `floatP`)
- New services are added
