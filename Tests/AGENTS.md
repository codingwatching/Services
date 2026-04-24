# GameLovers.Services Tests - AI Agent Guide

This file contains testing conventions for the `com.gamelovers.services` package. It is the source of truth when reading, editing, or creating test files under `Tests/`.

For runtime architecture, gotchas, and package-level context, see the parent [`AGENTS.md`](../AGENTS.md).

## 1. Placement Rules (EditMode vs PlayMode)
- **EditMode / Unit** (`EditMode/Unit/`): Pure-logic services with no `MonoBehaviour` or `GameObject` dependency. Use `[Test]`. NSubstitute is available (referenced only in the EditMode asmdef).
- **EditMode / Performance** (`EditMode/Performance/`): Perf benchmarks that do not need a running player. Require `PerformanceTestSetup` (see below).
- **PlayMode / Unit** (`PlayMode/Unit/`): Services that create `DontDestroyOnLoad` GameObjects (`TickService`, `CoroutineService`, `GameObjectPool`, `GameObjectPool<T>`). Use `[UnityTest]` returning `IEnumerator`.
- **PlayMode / Integration** (`PlayMode/Integration/`): Cross-service or async workflows (e.g., `VersionServicesIntegrationTest` loads resources).
- **PlayMode / Performance** (`PlayMode/Performance/`): Perf benchmarks that need a running player.
- **PlayMode / Smoke** (`PlayMode/Smoke/`): Lightweight "construct without throwing" tests that confirm services instantiate and basic bind/resolve works.

**Decision tree**: if the service under test creates a `GameObject` or relies on Unity callbacks → **PlayMode**; otherwise → **EditMode**.

## 2. Namespace and Suppression
All test files use `namespace GameLoversEditor.Services.Tests` with the suppression comment:
```csharp
// ReSharper disable once CheckNamespace
```

## 3. Naming
- **Test class**: `{ServiceName}Test` (e.g., `ObjectPoolTest`, `TickServiceTest`). Performance tests use `{ServiceName}PerformanceTest`. Integration tests use `{ServiceName}IntegrationTest`.
- **Test method**: `MethodOrBehavior_Condition_ExpectedResult` — e.g., `Spawn_Successfully`, `Range_MinEqualsMax_ReturnsMin`, `Despawn_NotSpawnedObject_ReturnsFalse`.
- **SetUp method**: Named `Init()`.
- **TearDown method**: Named `Dispose()` (when calling `service.Dispose()`) or `Cleanup()` (when doing `Object.Destroy` / `MainInstaller.Clean()`).

## 4. Mock / Helper Types
- Define mock interfaces and classes as **nested types** inside the test class (e.g., `IMockEntity`, `MockEntity`, `MockBehaviour`, `IMockSubscriber`).
- EditMode tests use **NSubstitute** (`Substitute.For<T>()`) for interface mocking. PlayMode tests use concrete `MonoBehaviour` stubs with manual counters (NSubstitute is not referenced in the PlayMode asmdef).

### NSubstitute limitation on Unity's Mono runtime
NSubstitute 4.4.0 (bundled Castle.Core DynamicProxy) cannot generate a proxy for a generic interface whose type argument is a **self-referentially-constrained interface**. Example: `Substitute.For<IObjectPool<IMockEntity>>()` where `IMockEntity : IPoolEntityObject<IMockEntity>` fails with `ArgumentNullException: localType` deep in `Castle.DynamicProxy.Generators.Emitters.SimpleAST.LocalReference.Generate` → `ILGenerator.DeclareLocal(null)`. Root cause is Castle's IL emitter resolving a generic parameter to `null` during type-building on Mono.

When a test would otherwise substitute such an interface, do ONE of:
- Use the real concrete implementation and verify via observable state (e.g., `new ObjectPool<IMockEntity>(...)` + assertions on `SpawnedReadOnly.Count`). This is preferred — see `EntityDespawn_Successfully` in `ObjectPoolTest`.
- Hand-write a minimal fake class implementing the interface.
Do not "work around" the proxy failure by restructuring the type hierarchy — `IMockEntity : IPoolEntityObject<IMockEntity>` is a legitimate modelling choice that the runtime code relies on.

## 5. Fields and Setup
- Fields are prefixed with `_` and use **concrete service types** (not interfaces): `private TickService _tickService;`, `private ObjectPool<IMockEntity> _pool;`.
- Constants use `PascalCase`: `private const int Seed = 12345;`.
- `[SetUp]` creates fresh service instances. Services that create GameObjects (`TickService`, `CoroutineService`) **must** call `Dispose()` in `[TearDown]`; `GameObjectPool` tests also `Object.Destroy` the sample GameObject.

## 6. Assertion Style
- NUnit classic model only: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow`, etc.
- No constraint-model (`Assert.That(...)`) usage in the existing suite.

## 7. Performance Tests
- Annotate with `[Test, Performance]` and `[Category("Performance")]`.
- Apply `[PrebuildSetup(typeof(PerformanceTestSetup))]` at the class level and call `PerformanceTestSetup.InitializePerformanceTestMetadata()` in `[OneTimeSetUp]`.
- Use `Measure.Method(() => { ... }).WarmupCount(n).MeasurementCount(n).Run()`.

## 8. Integration Tests
- Use `[Order(n)]` when tests must run in sequence (e.g., `VersionServicesIntegrationTest` resets static state, then loads, then reads).
- Reset shared static state in `[SetUp]` (reflection into private fields is acceptable for static classes like `VersionServices`).

## 9. Test Directory Layout

| Directory | Contents |
|-----------|----------|
| `EditMode/Unit/` | NUnit + NSubstitute; tests all non-MonoBehaviour services, incl. `AddressableConfigTest`, `AssetLoaderUtilsTest`, `AssetResolverServiceTest` |
| `EditMode/Performance/` | `Unity.PerformanceTesting`; ObjectPool, MessageBroker perf |
| `PlayMode/Unit/` | TickService, CoroutineService, GameObjectPool, GameObjectPool\<T\> (require a runtime) |
| `PlayMode/Integration/` | `ServiceLifecycleTest` full bootstrap/teardown, `VersionServicesIntegrationTest` async resource loading |
| `PlayMode/Performance/` | TickService, GameObjectPool perf |
| `PlayMode/Smoke/` | `ServicesBootstrapSmokeTest` |

### Note on `AddressablesAssetLoader` coverage
`AddressablesAssetLoader` is intentionally not covered by automated integration tests. It is a thin wrapper over `UnityEngine.AddressableAssets.Addressables` static APIs with no branching logic — every method is `LoadAssetAsync → ToUniTask → throw-on-failure → return`. Live integration would require a pre-built Addressables catalog plus a manually registered asset in the host project, and would validate Unity code rather than package code. The consumer layer (`AssetResolverService`) has full unit coverage via `AssetResolverServiceTest`, and the wrapper's behaviour is documented in `docs/asset-loading.md`.

## 10. Update Policy
Update this file when:
- Test conventions change (new asmdef references, assertion style, naming patterns, new test categories)
- New test directories or categories are added
- Mock/stub patterns change (e.g., NSubstitute added to PlayMode asmdef)
