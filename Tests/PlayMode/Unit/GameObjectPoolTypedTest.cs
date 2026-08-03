using System.Collections;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class GameObjectPoolTypedTest
	{
		public class MockBehaviour : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int>
		{
			public int SpawnCount;
			public int DespawnCount;
			public int LastSpawnData;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() => DespawnCount++;
			public void OnSpawn(int data) => LastSpawnData = data;
		}

		private GameObject _sampleGo;
		private MockBehaviour _sampleBehaviour;
		private GameObjectPool<MockBehaviour> _pool;

		[SetUp]
		public void Init()
		{
			_sampleGo = new GameObject("SampleTyped");
			_sampleBehaviour = _sampleGo.AddComponent<MockBehaviour>();
			_sampleGo.SetActive(false);
			_pool = new GameObjectPool<MockBehaviour>(0, _sampleBehaviour);
		}

		[TearDown]
		public void Cleanup()
		{
			_pool.Dispose();
			if (_sampleGo != null) Object.Destroy(_sampleGo);
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.Dispose(bool) destroyed SampleEntity.gameObject unconditionally — the same bug
		// already fixed on the non-generic GameObjectPool (see the sibling test in GameObjectPoolTest.cs).
		// RCR: GameObjectPool.cs GameObjectPool<T>.Dispose — revert to unconditional
		// `Object.Destroy(SampleEntity.gameObject);` → RED (_sampleGo destroyed with disposeSampleEntity: false). 2026-08-01
		public IEnumerator Dispose_WithDisposeSampleEntityFalse_DoesNotDestroySampleEntity()
		{
			_pool.Dispose(false);

			yield return null;

			Assert.IsFalse(_sampleGo == null);
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.SpawnEntity re-activates the pooled Behaviour's GameObject after the fake-null retry
		// loop.
		// RCR: GameObjectPool.cs GameObjectPool<T>.SpawnEntity — drop the `entity.gameObject.SetActive(true)` call → RED
		// (instance.gameObject.activeSelf is false). 2026-08-02
		public IEnumerator Spawn_ReturnsComponentReference()
		{
			var instance = _pool.Spawn();

			Assert.IsNotNull(instance);
			Assert.IsInstanceOf<MockBehaviour>(instance);
			Assert.AreNotSame(_sampleBehaviour, instance);
			Assert.IsTrue(instance.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.PostDespawnEntity deactivates the Behaviour's GameObject as it returns to the pool.
		// RCR: GameObjectPool.cs GameObjectPool<T>.PostDespawnEntity — drop the `entity.gameObject.SetActive(false)` call
		// → RED (activeSelf is still true). Also reddens DespawnAll and Despawn_WithCondition_FirstOnly. 2026-08-02
		public IEnumerator Despawn_DeactivatesGameObject()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			Assert.IsFalse(instance.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.CallOnDespawned resolves IPoolEntityDespawn through GetComponent and invokes OnDespawn.
		// RCR: GameObjectPool.cs GameObjectPool<T>.CallOnDespawned — drop the `poolEntity?.OnDespawn()` call → RED
		// (instance.DespawnCount stays 0). 2026-08-02
		public IEnumerator LifecycleHooks_InvokedOnSpawnAndDespawn()
		{
			var instance = _pool.Spawn();

			Assert.AreEqual(1, instance.SpawnCount);
			Assert.AreEqual(0, instance.DespawnCount);

			_pool.Despawn(instance);

			Assert.AreEqual(1, instance.DespawnCount);

			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.CallOnSpawned<TData> resolves IPoolEntitySpawn<TData> through GetComponent and forwards
		// the spawn payload.
		// RCR: GameObjectPool.cs GameObjectPool<T>.CallOnSpawned<TData> — drop the `poolEntity?.OnSpawn(data)` call → RED
		// (instance.LastSpawnData stays 0, not 42). 2026-08-02
		public IEnumerator SpawnWithData_InvokesTypedSpawnHook()
		{
			var instance = _pool.Spawn(42);

			Assert.AreEqual(42, instance.LastSpawnData);
			Assert.AreEqual(1, instance.SpawnCount);

			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.Dispose destroys the GameObject behind every Behaviour returned by Clear().
		// RCR: GameObjectPool.cs GameObjectPool<T>.Dispose — drop the `Object.Destroy(obj.gameObject)` call → RED (both
		// spawned instances survive). 2026-08-02
		public IEnumerator Dispose_DestroysAllSpawnedInstances()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			_pool.Dispose();

			yield return null;

			Assert.IsTrue(instance1 == null);
			Assert.IsTrue(instance2 == null);
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.DespawnAll walks the spawned list down to index 0, so the first-spawned entity is
		// despawned too.
		// RCR: ObjectPool.cs DespawnAll — change the loop bound to `i > 0` → RED (instance1 stays active and
		// SpawnedReadOnly.Count is 1). 2026-08-02
		public IEnumerator DespawnAll_DeactivatesAllSpawnedInstances()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			_pool.DespawnAll();

			Assert.IsFalse(instance1.gameObject.activeSelf);
			Assert.IsFalse(instance2.gameObject.activeSelf);
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			yield return null;
		}

		[UnityTest]
		public IEnumerator SampleEntity_ReturnsSampleReference()
		{
			Assert.AreSame(_sampleBehaviour, _pool.SampleEntity);

			yield return null;
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.SpawnedReadOnly exposes the live SpawnedEntities backing list, not a detached copy.
		// RCR: ObjectPool.cs SpawnedReadOnly — return a fresh empty list instead → RED (count stays 0 after Spawn). Also
		// reddens the other SpawnedReadOnly-count assertions in this fixture. 2026-08-02
		public IEnumerator SpawnedReadOnly_ReflectsSpawnedEntities()
		{
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			var instance = _pool.Spawn();

			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(instance, _pool.SpawnedReadOnly[0]);

			yield return null;
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.IsSpawned returns true for the first spawned entity that satisfies the predicate.
		// RCR: ObjectPool.cs IsSpawned — invert the predicate test → RED (matching returns false, non-matching returns
		// true). 2026-08-02
		public IEnumerator IsSpawned_ReturnsTrueWhenMatch()
		{
			var instance = _pool.Spawn();

			Assert.IsTrue(_pool.IsSpawned(e => e == instance));
			Assert.IsFalse(_pool.IsSpawned(e => false));

			yield return null;
		}

		[UnityTest]
		public IEnumerator Despawn_WithCondition_FirstOnly_Successfully()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: true, e => e == instance1));
			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
			Assert.IsFalse(instance1.gameObject.activeSelf);
			Assert.IsTrue(instance2.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.Despawn(bool, Func) steps the index back after a successful removal so adjacent matches
		// are not skipped.
		// RCR: ObjectPool.cs Despawn(bool, Func) — delete the `i--` step-back → RED (only the first of the two distinct
		// instances is despawned, count 1). 2026-08-02
		public IEnumerator Despawn_WithCondition_AllMatching_DespawnsAll()
		{
			_pool.Spawn();
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			yield return null;
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.Reset re-seeds _sampleEntity with the new sample before re-filling the stack.
		// RCR: ObjectPool.cs Reset — drop the `_sampleEntity = sampleEntity` assignment → RED (SampleEntity still points
		// at the old sample). 2026-08-02
		public IEnumerator Reset_ClearsAndReinitializesPool()
		{
			_pool.Spawn();

			var newSampleGo = new GameObject("NewSampleTyped");
			var newSample = newSampleGo.AddComponent<MockBehaviour>();
			newSampleGo.SetActive(false);

			_pool.Reset(2, newSample);

			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(newSample, _pool.SampleEntity);

			Object.Destroy(newSampleGo);
			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.PostDespawnEntity reparents a despawned instance under the sample entity's parent when
		// DespawnToSampleParent is set.
		// RCR: GameObjectPool.cs GameObjectPool<T>.PostDespawnEntity — drop the SetParent call → RED
		// (instance.transform.parent stays null, not the sample's parent). 2026-08-02
		public IEnumerator DespawnToSampleParent_ReparentsOnDespawn()
		{
			var parent = new GameObject("Parent");
			_sampleGo.transform.SetParent(parent.transform);

			var instance = _pool.Spawn();
			instance.transform.SetParent(null);

			_pool.Despawn(instance);

			Assert.AreSame(parent.transform, instance.transform.parent);

			// Detach before destroying parent so the cascade does not also destroy the
			// pooled instance (which the pool still tracks). Dispose-resilience to that
			// pattern is covered by Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow.
			_sampleGo.transform.SetParent(null);
			instance.transform.SetParent(null);

			Object.Destroy(parent);
			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.Dispose skips Unity fake-null entries because `.gameObject` on a destroyed Behaviour
		// throws MissingReferenceException.
		// RCR: GameObjectPool.cs GameObjectPool<T>.Dispose — delete the `if (obj == null) continue;` guard → RED
		// (MissingReferenceException, DoesNotThrow fails). 2026-08-02
		public IEnumerator Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow()
		{
			var externalParent = new GameObject("ExternalParent");
			_sampleGo.transform.SetParent(externalParent.transform);

			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			// PostDespawnEntity reparented `instance` under `externalParent`, so destroying
			// it cascades into both children while the pool still tracks `instance`.
			Object.Destroy(externalParent);
			yield return null;

			Assert.DoesNotThrow(() => _pool.Dispose());
		}

		[UnityTest]
		// ADMIT: GameObjectPool<T>.Dispose(true) destroys the GameObject behind the sample Behaviour.
		// RCR: GameObjectPool.cs GameObjectPool<T>.Dispose(bool) — drop the `Object.Destroy(SampleEntity.gameObject)` call
		// → RED (_sampleGo survives Dispose(true)). 2026-08-02
		public IEnumerator DisposeWithSampleDestroy_DestroysSampleGameObject()
		{
			_pool.Dispose(disposeSampleEntity: true);

			yield return null;

			Assert.IsTrue(_sampleGo == null);
		}
	}
}
