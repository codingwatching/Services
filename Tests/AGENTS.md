# GameLovers.Services Tests — AI Agent Guide

This file contains testing conventions for the `com.gamelovers.services` package. It is the source of truth when reading, editing, or creating test files under `Tests/`.

For runtime architecture, gotchas, and package-level context, see the parent [`AGENTS.md`](../AGENTS.md).

§1 and §2 are shared verbatim across every GameLovers package. A change to either must be applied to all six `Tests/AGENTS.md` files in the same working session, one commit per submodule.

## 1. ADMIT — Test Admission Test

A proposed test is admitted only if all five answers are YES. Record the first two
as comments on the test itself.

| | Question |
|---|---|
| **A1 DEFECT** | Can you name the defect in one sentence, referencing a production file and symbol? "It could break" is not a defect. |
| **A2 RED** | Can you name the exact production edit — one line or one branch, identified by `file` + `symbol` — that makes this test fail? If no such single edit exists, the test pins nothing. |
| **A3 PACKAGE** | Does every assertion read a value this package computed? Reject assertions on `new X() != new X()`, `!= null` on a freshly constructed object, default struct/enum values, or anything the C# spec or the Unity engine already guarantees. |
| **A4 CHEAPEST** | Is this the cheapest tier that covers the defect? EditMode beats PlayMode; a `[TestCase]` row on an existing fixture beats a new `[Test]`; a new `[Test]` beats a new fixture. Grep before writing. |
| **A5 UNIQUE** | Does no existing test already fail on the A2 edit? Grep the symbol under test across `Tests/` first. |
| **A6 ENVIRONMENT** | Would this assertion's outcome change if project configuration changed — a renderer feature installed or removed, an Addressables catalog built, a sample imported, a quality tier switched? If yes, the test must **read** that state, not assume one value of it. |

**A6 in practice.** A6 is not A3. A3 asks whether the package computed the value; A6
asks whether the test assumed which value it would be. A test can satisfy A3 and still
fail A6 — reading a package-computed flag is fine, hard-coding the expectation that the
flag is `false` is not.

The concrete instance: `UiBackdropBlurPresenterFeatureTests` unconditionally expected
the "no renderer feature installed" error. Production only logs it when
`UiBackdropBlurRendererFeature.IsInstalled` is false. Batchmode never instantiates the
URP renderer, so the flag was false and all five tests passed; in the Editor the feature
registers from the project's renderer asset, the flag is true, production correctly stays
silent, and all five failed. The fixture was really asserting *"this project has no blur
renderer feature"* — a fact about the repo, not about the code under test.

The fix shape is always the same: branch the expectation on the state instead of assuming
it, and leave the assertions that are actually the subject untouched.

```csharp
if (UiBackdropBlurRendererFeature.IsInstalled) return;   // production logs nothing
LogAssert.Expect(LogType.Error, ...);
```

If a test genuinely needs one specific value of ambient state, it must establish that
state itself in `[SetUp]` and restore it in `[TearDown]` — never inherit it.

**A5-bis — inherited-type coverage.** Before proposing a fixture for a type that
derives from or wraps another tested type, grep `Tests/` for the derived type's
name and for paired `[SetUp]` fields. Base-and-derived pairs are tested jointly in
the base's fixture unless the derived type adds new public surface.

**Two mechanical disqualifiers** — violate one and the test is rejected:

- **D1 — tautology.** If the only assertion is `Assert.DoesNotThrow`,
  `Assert.IsNotNull`, or a disjunction of `Contains(...)` substrings, the test
  fails A2 unless you write down what *would* throw, be null, or not match. A
  substring disjunction that includes a string the input itself embeds is
  unfalsifiable by construction.
- **D2 — name/body contract.** The test name is a claim. If deleting the
  production feature the name mentions leaves the test green, the name is a lie.

**Smoke exemption, by directory.** Fixtures under `Smoke/` are exempt from A1 and
A2 and may assert construction-without-throwing only. Their defect class is "the
assembly no longer loads / bootstrap regressed", which is real and not expressible
otherwise. The exemption is by directory, not by assertion shape — a Unit test
that only asserts `IsNotNull` is still rejected.

## 2. RCR — Revert and Confirm Red

> Every new or strengthened test must be observed failing, once, against a
> one-line production revert, before it is committed.

Line coverage proves a line executed. It does not prove any test would notice if
that line were wrong. RCR is the cheap substitute for mutation testing, and it is
what makes a coverage number trustworthy.

**Procedure** (~90 seconds per test):

1. Write the test. Run it. Green.
2. Apply the A2 edit — invert the comparison, delete the guard clause, return
   early, comment out the one line. **One line only**: a broad deletion proves
   nothing, because it would also "fail" a tautological test via a compile error.
3. Run only that test. It must be **RED**, and the failure message must name the
   thing you broke. A red-by-`NullReferenceException` does not count — that is the
   test crashing, not asserting.
4. `git checkout -- <production file>`. Re-run. Green.
5. Record the mutation in the test's header comment.

**Recording format** — on the test, not in a separate ledger. A ledger rots the
moment a test is renamed; a comment travels with the test, appears in every diff
that touches it, and lets a reviewer re-run the mutation in 30 seconds.

```csharp
[Test]
// ADMIT: <one-sentence defect, naming a production file and symbol>
// RCR:   <file> <symbol> — <the one-line mutation> → RED (<what the failure says>). <YYYY-MM-DD>
public void Method_Condition_ExpectedResult()
```

**Anchor on `file` + `symbol`, never `file:line`.** Line numbers rot on the first
unrelated edit above them — a stale `:474` pointing at a method that moved to `:464`
sends the next reader to the wrong code and quietly destroys the comment's value.

**Budget: four lines is the target, six is the ceiling.** One sentence of ADMIT,
one of RCR, wrapped. This obeys the repo-wide rule in the root `AGENTS.md`
(§ Code comments): *"One sentence usually suffices. Multi-paragraph rationale is a
smell."* Anything past the ceiling belongs in the commit body or `docs/`, not on the
test. Two things in particular must NOT appear here:
- **Change narration.** *"An earlier version of this test was a tautology"* is diff
  context; the root `AGENTS.md` forbids it outright. A comment states the code's
  permanent condition, not its history. Put it in the commit message.
- **Investigation transcript.** The empirical detail that convinced *you* is not
  what the next reader needs. They need the mutation and the expected failure.

The one extension worth its lines is a **negative** result: naming a nearby edit
that looks like a valid mutation but is NOT one (because it is already guarded, or
because it reddens a sibling test instead). That stops the next reader repeating a
dead end, and it cannot be recovered from the code.

Also add one line per new test to the commit body: `RCR: <TestName> ← <file> <symbol> <mutation>`.
That makes `git log --grep=RCR` the audit surface.

**UNFALSIFIABLE — the one honest exemption.** Some correct tests provably have no
one-line mutation. The commonest case is **double-guarded validation**: an
unconfigured object trips two independent guards, so disabling either leaves the
other throwing. Deleting such a test would lose real coverage, so it is exempt —
but only on the same terms as §13, never as a shrug:

```csharp
// RCR: none exists — <input> trips both <guard A> and <guard B>; disabling either
// leaves the other throwing (verified). Double-covered, not single-line falsifiable.
```

The reason must be falsifiable and must record that a mutation was actually tried
and observed green. "Couldn't find one" is not a reason — that is an unfinished RCR,
not an exemption.

**Verdicts for a test that resists mutation.** Work out which of four it is; they
have different answers:

| Finding | Test | Action |
|---|---|---|
| **A3 reject** — no line in `Runtime/` or `Editor/` participates; the assertion is C#- or Unity-guaranteed | pins nothing, ever | **Delete** |
| **A5 duplicate** — the only mutation that reddens it already belongs to a sibling | pins nothing new | **Delete**, naming the surviving sibling in the commit body |
| **D2 overclaim** — the name promises behaviour the body cannot detect | name is a lie | **Strengthen the assertion**, or rename to what it actually checks |
| **UNFALSIFIABLE** — real behaviour, but double-guarded or otherwise unbreakable one line at a time | valid | **Keep**, with the exemption comment above |
| **SHARED-PATH** — no unique one-line pin, but the test was *observed* reddening under a broader mutation | valid | **Keep**, recording the covering mutation and its blast radius |

**SHARED-PATH exists because blast radius measures specificity, not value.** A test that
only reddens under a broad mutation still catches that regression — an integration test
that dies when `UiService.CloseUi` is gutted is doing its job, even though no single line
is *its* line. Without this row the table offers only delete-or-strengthen, and such tests
get deleted for the crime of being integration tests.

```csharp
// ADMIT: exercises <Production.Symbol>'s <path>; no unique one-line pin.
// RCR: no isolated mutation — reddens under <SiblingTest>'s mutation (radius N, verified).
// Shared-path coverage, not a duplicate.
```

The radius must be a **recorded observation**, not an estimate. This is also the row most
easily abused: "some mutation somewhere reddened it" is not the standard. Distinguish it
from A5 by asking what the covering mutation actually broke — if it broke the one narrow
guard the sibling owns, this is a duplicate; if it broke a path both tests legitimately
traverse, this is shared-path coverage.

A cluster of tests that all die to the same broad mutation is **over-provisioned, not
individually worthless**. Thinning it is a deliberate editorial decision made by a human
looking at what each assertion adds — never an automatic consequence of the verdict pass.

**A3 is checked first, and it is the commonest way the exemption gets abused.**
UNFALSIFIABLE is for behaviour this package genuinely owns but cannot be broken one
line at a time. It is *never* for behaviour the package does not own. The tell is in
the reason itself: if you find yourself writing "no line in Runtime/ participates",
"the only edit is a compile error", or "these are C#'s zero-init values", you have
found an A3 reject and the verdict is **delete** — a field-only struct's assignment
and default values are the language's guarantees, not yours. Writing that sentence
under an UNFALSIFIABLE heading launders a test §1 would never have admitted.

Prove the class before acting. An A5 duplicate is confirmed when the sibling's
mutation is observed reddening both; a D2 overclaim is confirmed when the mutation
the name implies leaves the test green; an A3 reject is confirmed when no production
symbol appears anywhere in the causal chain behind the assertion.

**Two consequences, stated so RCR does not become theatre:**

- A test with no `// RCR:` line — and no UNFALSIFIABLE exemption — is not trusted
  coverage. In an audit it is a suspect by default. **`Smoke/` is exempt here too**, on the
  same directory basis as §1: its defect class is "the assembly no longer loads", which has
  no one-line mutation, so demanding an RCR line there flags those fixtures forever. The
  exemption is the directory, not the assertion shape.
- **"Unannotated" is three states, not one, and they need different actions.** A test with no
  `// RCR:` line may have been (a) observed RED with the write-back lost, (b) seen reddening
  only as collateral inside another test's blast radius, or (c) never probed. Only (c) needs a
  probe; (a) needs the recorded observation written back; (b) is SHARED-PATH evidence, not a
  unique pin. Check `.test-all/rcr/` before probing, and never write prepared annotation text
  without a matching `RED-OK` for that test — prepared text also exists for tests that were
  never probed, and writing it fabricates a verified claim.
- **Benchmarks are included, inverted:** a performance test must be observed
  *changing its number* when the measured operation is removed from the measured
  body. A benchmark whose measured region does not contain the workload is a
  tautology in `Measure` clothing.

## 3. Placement Rules (EditMode vs PlayMode)
- **EditMode / Unit** (`EditMode/Unit/`): Pure-logic services with no `MonoBehaviour` or `GameObject` dependency. Use `[Test]`. NSubstitute is available (referenced only in the EditMode asmdef).
- **EditMode / Performance** (`EditMode/Performance/`): Perf benchmarks that do not need a running player. Require `PerformanceTestSetup` (see below).
- **PlayMode / Unit** (`PlayMode/Unit/`): Services that create `DontDestroyOnLoad` GameObjects (`TickService`, `CoroutineService`, `GameObjectPool`, `GameObjectPool<T>`). Use `[UnityTest]` returning `IEnumerator`.
- **PlayMode / Integration** (`PlayMode/Integration/`): Cross-service or async workflows that span multiple bound services or exercise a real load path (e.g., full service bootstrap/teardown sequences, async resource loading).
- **PlayMode / Performance** (`PlayMode/Performance/`): Perf benchmarks that need a running player.
- **PlayMode / Smoke** (`PlayMode/Smoke/`): Lightweight "construct without throwing" tests that confirm services instantiate and basic bind/resolve works.

**Decision tree**: if the service under test creates a `GameObject` or relies on Unity callbacks → **PlayMode**; otherwise → **EditMode**.

## 4. Namespace and Suppression
All test files use `namespace GameLoversEditor.Services.Tests` with the suppression comment:
```csharp
// ReSharper disable once CheckNamespace
```

## 5. Naming
- **Test class**: `{ServiceName}Test` (e.g., `ObjectPoolTest`, `TickServiceTest`). Performance tests use `{ServiceName}PerformanceTest`. Integration tests use `{ServiceName}IntegrationTest`.
- **Test method**: `MethodOrBehavior_Condition_ExpectedResult` — e.g., `Spawn_Successfully`, `Range_MinEqualsMax_ReturnsMin`, `Despawn_NotSpawnedObject_ReturnsFalse`.
- **SetUp method**: Named `Init()`.
- **TearDown method**: Named `Dispose()` (when calling `service.Dispose()`) or `Cleanup()` (when doing `Object.Destroy` / `MainInstaller.Clean()`).

## 6. Mock / Helper Types
- Define mock interfaces and classes as **nested types** inside the test class (e.g., `IMockEntity`, `MockEntity`, `MockBehaviour`, `IMockSubscriber`).
- EditMode tests use **NSubstitute** (`Substitute.For<T>()`) for interface mocking. PlayMode tests use concrete `MonoBehaviour` stubs with manual counters (NSubstitute is not referenced in the PlayMode asmdef).

### NSubstitute limitation on Unity's Mono runtime
NSubstitute 4.4.0 (bundled Castle.Core DynamicProxy) cannot generate a proxy for a generic interface whose type argument is a **self-referentially-constrained interface**. Example: `Substitute.For<IObjectPool<IMockEntity>>()` where `IMockEntity : IPoolEntityObject<IMockEntity>` fails with `ArgumentNullException: localType` deep in `Castle.DynamicProxy.Generators.Emitters.SimpleAST.LocalReference.Generate` → `ILGenerator.DeclareLocal(null)`. Root cause is Castle's IL emitter resolving a generic parameter to `null` during type-building on Mono.

When a test would otherwise substitute such an interface, do ONE of:
- Use the real concrete implementation and verify via observable state (e.g., `new ObjectPool<IMockEntity>(...)` + assertions on `SpawnedReadOnly.Count`). This is preferred — see `EntityDespawn_Successfully` in `ObjectPoolTest`.
- Hand-write a minimal fake class implementing the interface.
Do not "work around" the proxy failure by restructuring the type hierarchy — `IMockEntity : IPoolEntityObject<IMockEntity>` is a legitimate modelling choice that the runtime code relies on.

## 7. Black-Box / Reflection Policy

### Authorized reflection sites (storage-assertion exception)
Reflection on private state is also authorized when a setter has no observable readback path through the public API and exercising the side-effect would require a runtime environment the EditMode harness cannot provide (e.g., a partially-loaded `AssetReference`). In those cases, asserting the storage field directly via `BindingFlags.NonPublic | BindingFlags.Instance` is acceptable and preferable to a Red-testability skip. The test method MUST be a single setter-storage assertion (not a multi-step behavioural assertion); if behaviour is what you need to verify, refactor to expose an `internal` accessor under `InternalsVisibleTo` instead.

Currently authorized:
- `AssetResolverServiceTest.AddDebugConfigs_StoresAllProvided` — reads the private `AssetResolverService._errorMaterial` field to confirm `AddDebugConfigs` stored its argument. The fallback-material lookup path (`AssetResolverService.SelectAsset<TAsset>` in `Runtime/AssetResolverService.cs`, called by the private `Convert<TAsset>` wrapper) only fires when `!assetReference.IsDone`, which the EditMode harness cannot fabricate without a real Addressables catalog. Documented here per the Type B audit run on 2026-05-04 (Referee §4 missed-anti-pattern finding, parent picked option A).

## 8. Fields and Setup
- Fields are prefixed with `_` and use **concrete service types** (not interfaces): `private TickService _tickService;`, `private ObjectPool<IMockEntity> _pool;`.
- Constants use `PascalCase`: `private const int Seed = 12345;`.
- `[SetUp]` creates fresh service instances. Services that create GameObjects (`TickService`, `CoroutineService`) **must** call `Dispose()` in `[TearDown]`; `GameObjectPool` tests also `Object.Destroy` the sample GameObject.
- Use `[Order(n)]` when tests must run in sequence (e.g., `VersionServicesIntegrationTest` resets static state, then loads, then reads).
- Reset shared static state in `[SetUp]` (reflection into private fields is acceptable for static classes like `VersionServices`).

## 9. Assertion Style
- NUnit classic model is the **default**: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow`, etc.
- `Assert.That(...)` (constraint model) is permitted **ONLY** for tolerance/range constraints that classic asserts cannot express (`Is.EqualTo(x).Within(t)`, `Is.LessThan(x)`, and similar). The only authorized sites are `TimeServiceTest.cs:67,83`. Any other use is a review reject.

## 10. PlayMode Test Cleanup

This package's `PlayMode/Unit/` and `PlayMode/Integration/` fixtures create `DontDestroyOnLoad` GameObjects (`TickService`, `CoroutineService`, `GameObjectPool`, `GameObjectPool<T>`) and bind services through `MainInstaller`. These survive scene teardown between tests unless explicitly torn down, and will leak into the next test's domain if left alive.

- `[TearDown]` **must** call `Dispose()` on any `TickService` / `CoroutineService` instance created in `[SetUp]` — this destroys the host `DontDestroyOnLoad` GameObject.
- `GameObjectPool` / `GameObjectPool<T>` tests must additionally `Object.Destroy` the sample GameObject (or call `Dispose(disposeSampleEntity: true)`) so no pooled instances survive into the next test.
- Fixtures that bind through `MainInstaller.Bind<T>(...)` (e.g. `ServiceLifecycleTest`, `VersionServicesIntegrationTest`) **must** call `MainInstaller.Clean()` in `[TearDown]` to clear bindings; a missing `Clean()` call causes the next fixture's `Bind<T>` call to throw (`Installer` re-bind throws via `Dictionary.Add`).
- Do not rely on domain reload or Unity's own scene-unload to clean these up between tests — the Unity Test Runner does not guarantee a domain reload between every test in a fixture.

## 11. Performance Tests
- Annotate with `[Test, Performance]` and `[Category("Performance")]`.
- Apply `[PrebuildSetup(typeof(PerformanceTestSetup))]` at the class level and call `PerformanceTestSetup.InitializePerformanceTestMetadata()` in `[OneTimeSetUp]`.
- Use `Measure.Method(() => { ... }).WarmupCount(n).MeasurementCount(n).Run()`.

### `PerformanceTestSetup` PlayerPref contract (do NOT regress)
`InitializePerformanceTestMetadata()` MUST prime **two** PlayerPref keys before any `Measure.Method(...).Run()` call — dropping either one ships a latent NRE that masks the actual perf-test logic:
- `PT_Run` — full Run metadata (editor info, dependencies, build settings); consumed by `Metadata.SetRuntimeSettings()` when results are emitted.
- `PT_Settings` — RunSettings JSON (use `"{\"MeasurementCount\":-1}"`); consumed by `MethodMeasurement.SettingsOverride()` *before* the first warmup.

Why both keys: `RunSettings.Instance` is a lazy-loaded singleton (`ResourcesLoader.Load<RunSettings>("PerformanceTestRunSettings", "PT_Settings")`). In Editor it falls back to `PlayerPrefs.GetString("PT_Settings")`; if the value is empty, `JsonUtility.FromJson` throws, the loader silently swallows the exception and returns `null`, and `SettingsOverride()` then NREs at `RunSettings.Instance.MeasurementCount`. The failure surfaces at `MethodMeasurement.cs:288` with no hint that the setup is incomplete.

`MeasurementCount = -1` is the package's "no override" sentinel — `SettingsOverride()` early-returns when `count < 0`, so each fixture's per-test `WarmupCount(...).MeasurementCount(...)` is preserved.

`PerformanceTestSetupTest.MeasureMethod_AfterInitialize_DoesNotThrow` is the regression sentinel for this contract: a no-op `Measure.Method(() => {}).WarmupCount(1).MeasurementCount(1).Run()` wrapped in `Assert.DoesNotThrow`. If a future change to `PerformanceTestSetup` drops either PlayerPref, this test fails first with a class name that points directly at the harness — keep it green.

## 12. Test Directory Layout

| Directory | Contents |
|-----------|----------|
| `EditMode/Unit/` | NUnit + NSubstitute; tests all non-MonoBehaviour services, incl. `AddressableConfigTest`, `AssetLoaderUtilsTest`, `AssetResolverServiceTest` |
| `EditMode/Performance/` | `Unity.PerformanceTesting`; ObjectPool, MessageBroker perf |
| `PlayMode/Unit/` | TickService, CoroutineService, GameObjectPool, GameObjectPool\<T\> (require a runtime) |
| `PlayMode/Integration/` | `ServiceLifecycleTest` full bootstrap/teardown, `VersionServicesIntegrationTest` async resource loading |
| `PlayMode/Performance/` | TickService, GameObjectPool perf |
| `PlayMode/Smoke/` | `ServicesBootstrapSmokeTest` |

## 13. Coverage Register

**Baseline — runtime assembly: 84.4% (1050/1244), measured 2026-08-04.**
Editor assembly: **4.8% (153/3162)** — near-zero by policy; the ACCEPTED (iii) rows below are why.
Repo-wide runtime coverage is **74.1% (6609/8922)** across all 11 assemblies.

Regenerate with `Tools/coverage.sh`, which prints the runtime/Editor split. Steer by
the **runtime** figure: Editor code is ~48% of coverable lines and accepted-untestable,
so the combined number (41.1%) can never meaningfully move. Sanity-check any rerun by
confirming `MathfloatP` reports ~1002 coverable lines — a smaller figure means
`-debugCodeOptimization` was missing and the denominator silently shrank ~40%.


Every untested symbol worth naming is ACCEPTED (justified — do not re-report),
OPEN (a real gap, owed a test), or CLOSED (the gap was filled). An untested symbol
in none of the three is an audit finding.

**A CLOSED row must name the commit AND the observation that closed it, including the
environment the observation came from.** A row closed on "the fix landed" is still OPEN:
the fix is the edit, the closure is the evidence. This is what kept the uiservice A6 row
open until the Editor half ran — the edit was in and batchmode was green, and neither of
those was the thing in doubt.

**Closing a row means re-deriving its claim against current source, never reading the
commit that claimed to fix it.** Re-check every symbol and fixture the row names. A partial
fix and a complete one produce the same green suite and the same confident commit message,
so the commit cannot be the evidence for its own completeness. Recorded instance: the
mobileservices editor-static row nearly closed on a commit that genuinely did stop fixtures
inheriting statics — for two of the three fixtures the row named. The third was found by
grepping which fixtures touch each static, and it was passing only because its siblings
happened to restore the static in their `finally` blocks.

An ACCEPTED row needs one of exactly three falsifiable reasons:
- **(i) no branching** — zero conditionals, so there is no behaviour to pin.
- **(ii) engine-owned** — the assertion would target Unity/OS behaviour
  (`[DllImport]`, `AndroidJavaObject`, Addressables statics).
- **(iii) harness-impossible** — the state cannot be fabricated in EditMode or
  PlayMode, **with the specific blocker named**.

"Low value", "hard to test", and "covered by manual QA" are NOT valid reasons. If
none of the three applies, the row is OPEN.

ACCEPTED is dated and **expires on edit**: if the symbol's file changes, the
reason is re-checked in that PR. A `(i) no branching` row is void the moment
someone adds an `if`.

OPEN is the only place a deletion may park coverage. A test removed for weakness
either had a stronger sibling (named in the commit body) or leaves an OPEN row.
The count of OPEN rows is the honest coverage-debt number.

| Symbol (file:line) | State | Reason / Owed | Recorded |
|---|---|---|---|
| `AddressablesAssetLoader` (`Runtime/AssetsImporter/AddressablesAssetLoader.cs`) | ACCEPTED | (i) no branching — thin wrapper over `UnityEngine.AddressableAssets.Addressables` static APIs with no branching logic: every method is `LoadAssetAsync → ToUniTask → throw-on-failure → return`. Live integration would require a pre-built Addressables catalog plus a manually registered asset in the host project, and would validate Unity code rather than package code. The consumer layer (`AssetResolverService`) has full unit coverage via `AssetResolverServiceTest`, and the wrapper's behaviour is documented in `docs/asset-loading.md`. | 2026-07-31 |
| 25 public `Editor/` types + `ServicesScaffolders` (`Editor/**`) | ACCEPTED | (iii) harness-impossible — blocker: require `AssetDatabase` access, validated manually. Already stated in the package root [`AGENTS.md`](../AGENTS.md) §4 ("AssetsConfigsImporter (Editor)"); cross-referenced here rather than restated. | 2026-07-31 |
| `ServicesScaffolders` `#if UNITY_6000_4_OR_NEWER` guard (`Editor/Scaffolders/ServicesScaffolders.cs`) | ACCEPTED | (iii) harness-impossible — blocker: compile-time branch, only one side is reachable per Unity version, so a single test run can only ever exercise one branch of the `#if`. | 2026-07-31 |
| `VersionServices.IsOutdatedVersion` (`Runtime/VersionServices.cs`) | OPEN | Owed: only coverage was a local reimplementation of the algorithm in `VersionServicesTest.cs`; a real test against the actual method is owed. | 2026-07-31 |
| `AddressableIdsGeneratorUtils.ResolveSanitizedEnumName` 3-way collision (`Editor/AddressableIds/AddressableIdsGeneratorUtils.cs:531-539`) | OPEN | The two-address collision was fixed 2026-08-01 (`AppendAddressEnumMembers` now emits the disambiguated `name`). A deeper edge case remains: a THIRD address colliding with the same base name AND filetype re-derives the identical `"{name}_{filetype}"` suffix (the fallback only ever adds one suffix level and the collision check is against the original `name`, not against previously-suffixed candidates), so 3+ colliding addresses can still emit duplicates. Owed: either a numeric fallback (`_2`, `_3`, ...) or checking collision against the full history of emitted names, not just base names. | 2026-08-01 |
| `ObjectPoolBase<T>.Despawn` `onlyFirst` break (`Runtime/Pooling/ObjectPool.cs`) | OPEN | Owed: the branch is uncovered **repo-wide**, proven not inferred — inverting `if (onlyFirst)` left `Despawn_WithCondition_FirstOnly_Successfully` GREEN (the EditMode fixture spawns one entity; the PlayMode typed sibling's predicate matches only one). A test must spawn two matching entities and assert exactly one is despawned. | 2026-08-04 |
| `TimeService.UnityTimeFromDateTimeUtc` / `UnixTimeFromDateTimeUtc` shrink direction (`Runtime/TimeService.cs`) | CLOSED | Closed 2026-08-04 by `07abdf1`: the three assertion pairs now bound `Math.Abs(converted - now)` from both sides. The 1000x `TotalMilliseconds`→`TotalSeconds` shrink and the `_initialUnityTime` negation are now both observed RED (unix comparisons use a separate `UnixErrorMillis`, since they are in milliseconds). | 2026-08-04 |
| `RngService.Peekfloat` state-advance detection (`Runtime/RngService.cs`) | OPEN | Owed: `Peekfloat` draws over `(0, floatP.MaxValue)`, where consecutive draws saturate to the same `floatP` — so making `PeekRange` consume the LIVE state was observed GREEN. The test is precision-blind to the advance its name claims to catch. | 2026-08-04 |
| 64 production edits reddening only collaterally (`Runtime/RngService.cs`, `Runtime/CoroutineService.cs`, `Runtime/TickService.cs`) | OPEN | Measured 2026-08-04 from `.test-all/rcr/unowned-edits.json`: 64 edits produced RED but never an `isolated` verdict — `RngService.cs` (11), `CoroutineService.cs` (9), `TickService.cs` (7), `ObjectPool.cs` (6). **Not 64 missing tests**; see the gamedata row for the reasoning. Owed: a judgement pass on whether the deterministic-RNG and tick-fan-out paths warrant narrow pins, since both are shared setup for many fixtures. | 2026-08-04 |
| `TickServiceTest.MultipleInstances_CreateMultipleGameObjects` exemption is an unfinished RCR (`Runtime/TickService.cs`) | CLOSED | Closed 2026-08-04. The one-line mutation exists and was run: `private readonly TickServiceMonoBehaviour _tickObject;` -> `private static ...` makes production's own ctor guard fire, and the test went RED with `InvalidOperationException: The tick service is being initialized for the second time`. Observed in the **Editor**; `TickService.cs` restored byte-identical. Also removed 15 lines of narration and commented-out production code from the test body. See the new dead-guard row below. | 2026-08-04 |
| `TickService` ctor singleton guard is unreachable dead code (`Runtime/TickService.cs`) | CLOSED | Closed 2026-08-04: the dead guard is deleted (`CHANGELOG.md` **Fixed**), which also makes `TickService` consistent with `CoroutineService` — same host-spawning shape, never carried such a guard. `MultipleInstances_CreateMultipleGameObjects` was re-pointed, because removing the guard voided its previous mutation: with the guard gone, `_tickObject` `readonly`->`static` was observed leaving the test **GREEN**. The replacement pins the ctor's own `AddComponent` — routing it through `Object.FindAnyObjectByType<TickServiceMonoBehaviour>() ?? ...` (a simulated singleton) was observed RED with "Expected: greater than or equal to 2". Green -> RED -> green, `TickService.cs` restored to the guard-deleted state (md5 verified). | 2026-08-04 |

## 14. Update Policy
Update this file when:
- Test conventions change (new asmdef references, assertion style, naming patterns, new test categories)
- New test directories or categories are added
- Mock/stub patterns change (e.g., NSubstitute added to PlayMode asmdef)
