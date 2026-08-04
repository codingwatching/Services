using System.Collections;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class GameObjectPoolTest
	{
		public class MockPoolEntity : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn
		{
			public int SpawnCount;
			public int DespawnCount;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() => DespawnCount++;
		}

		public class MockPoolEntityWithData : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int>
		{
			public int SpawnCount;
			public int LastSpawnData;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() {}
			public void OnSpawn(int data) => LastSpawnData = data;
		}

		private GameObject _sample;
		private GameObjectPool _pool;

		[SetUp]
		public void Init()
		{
			_sample = new GameObject("Sample");
			_sample.AddComponent<MockPoolEntity>();
			_sample.SetActive(false);
			_pool = new GameObjectPool(0, _sample);
		}

		[TearDown]
		public void Cleanup()
		{
			_pool.Dispose(true);
			if (_sample != null) Object.Destroy(_sample);
		}

		[UnityTest]
		// ADMIT: GameObjectPool.SpawnEntity re-activates the pooled GameObject that Instantiator deactivated on creation.
		// RCR: GameObjectPool.cs GameObjectPool.SpawnEntity — drop the `entity.SetActive(true)` call → RED
		// (instance.activeSelf is false). 2026-08-02
		public IEnumerator Spawn_InstantiatesPrefab()
		{
			var instance = _pool.Spawn();
			
			Assert.IsNotNull(instance);
			Assert.AreNotSame(_sample, instance);
			Assert.IsTrue(instance.activeSelf);
			
			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool.PostDespawnEntity deactivates the GameObject as it returns to the pool.
		// RCR: GameObjectPool.cs GameObjectPool.PostDespawnEntity — drop the `entity.SetActive(false)` call → RED
		// (instance.activeSelf is still true after Despawn). 2026-08-02
		public IEnumerator Despawn_DeactivatesGameObject()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);
			
			Assert.IsFalse(instance.activeSelf);
			
			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool.CallOnSpawned resolves IPoolEntitySpawn through GetComponent and invokes OnSpawn on the
		// pooled instance.
		// RCR: GameObjectPool.cs GameObjectPool.CallOnSpawned — drop the `poolEntity?.OnSpawn()` call → RED
		// (mock.SpawnCount stays 0). 2026-08-02
		public IEnumerator Spawn_InvokesIPoolEntitySpawn()
		{
			var instance = _pool.Spawn();
			var mock = instance.GetComponent<MockPoolEntity>();
			
			Assert.AreEqual(1, mock.SpawnCount);
			
			_pool.Despawn(instance);
			Assert.AreEqual(1, mock.DespawnCount);
			
			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool.Dispose destroys every GameObject returned by Clear(), spawned ones included.
		// RCR: GameObjectPool.cs GameObjectPool.Dispose — drop the `Object.Destroy(obj)` call → RED (the spawned instance
		// is still alive next frame). 2026-08-02
		public IEnumerator Dispose_DestroysAllInstances()
		{
			var instance = _pool.Spawn();
			_pool.Dispose();
			
			// Note: Object destruction is delayed until end of frame or next frame
			yield return null;
			
			Assert.IsTrue(instance == null);
		}

		[UnityTest]
		// ADMIT: GameObjectPool.Dispose(true) destroys the sample entity the pool was seeded with.
		// RCR: GameObjectPool.cs GameObjectPool.Dispose(bool) — drop the `Object.Destroy(SampleEntity)` call → RED
		// (_sample survives Dispose(true)). 2026-08-02
		public IEnumerator Dispose_WithSampleDestroy_DestroysSample()
		{
			_pool.Dispose(true);
			
			yield return null;
			
			Assert.IsTrue(_sample == null);
		}

		[UnityTest]
		// ADMIT: GameObjectPool.CallOnSpawned<TData> resolves IPoolEntitySpawn<TData> through GetComponent and forwards
		// the spawn payload.
		// RCR: GameObjectPool.cs GameObjectPool.CallOnSpawned<TData> — drop the `poolEntity?.OnSpawn(data)` call → RED
		// (mock.LastSpawnData stays 0, not 42). 2026-08-02
		public IEnumerator SpawnWithData_InvokesIPoolEntitySpawn()
		{
			var sampleWithData = new GameObject("SampleWithData");
			sampleWithData.AddComponent<MockPoolEntityWithData>();
			sampleWithData.SetActive(false);
			var poolWithData = new GameObjectPool(0, sampleWithData);

			var instance = poolWithData.Spawn(42);
			var mock = instance.GetComponent<MockPoolEntityWithData>();

			Assert.AreEqual(42, mock.LastSpawnData);

			poolWithData.Dispose(true);

			yield return null;
		}

		[UnityTest]
		// ADMIT: GameObjectPool.Dispose must use the Unity fake-null guard (`obj == null`) on every Clear() entry —
		// a pooled instance can be destroyed by an external parent while the pool still tracks it.
		// RCR: GameObjectPool.cs GameObjectPool.Dispose() — `if (obj == null)` → `if (obj.transform == null)` → RED
		// (MissingReferenceException from dereferencing the destroyed GameObject). 2026-08-02
		public IEnumerator Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow()
		{
			var externalParent = new GameObject("ExternalParent");
			_sample.transform.SetParent(externalParent.transform);

			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			// PostDespawnEntity reparented `instance` under `externalParent`, so destroying
			// it cascades into both children while the pool still tracks `instance`.
			Object.Destroy(externalParent);
			yield return null;

			Assert.DoesNotThrow(() => _pool.Dispose());
		}

		[UnityTest]
		// ADMIT: GameObjectPool.Dispose(bool) destroyed SampleEntity unconditionally, ignoring disposeSampleEntity,
		// so Dispose(false) still destroyed a sample entity the caller explicitly asked to keep.
		// RCR: GameObjectPool.cs Dispose — revert to unconditional `Object.Destroy(SampleEntity);` → RED (the
		// sample entity is destroyed even with disposeSampleEntity: false). 2026-08-01
		public IEnumerator Dispose_WithDisposeSampleEntityFalse_DoesNotDestroySampleEntity()
		{
			_pool.Dispose(false);

			yield return null;

			Assert.IsFalse(_sample == null);
		}

		[UnityTest]
		// ADMIT: ObjectPoolBase<T>.SpawnEntity retries popping while the popped entity is Unity fake-null, so a
		// pooled GameObject destroyed by an external owner while despawned is never handed back out.
		// RCR: ObjectPool.cs SpawnEntity — collapse the do-while retry to a single unconditional pop → RED (Spawn
		// returns the destroyed instance; IsFalse(freshInstance == null) fails). 2026-08-01
		public IEnumerator Spawn_WhenPooledEntityWasDestroyedExternally_ReturnsFreshInstance()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			Object.DestroyImmediate(instance);

			var freshInstance = _pool.Spawn();

			Assert.IsFalse(freshInstance == null);
			Assert.AreNotSame(instance, freshInstance);

			yield return null;
		}
	}
}
