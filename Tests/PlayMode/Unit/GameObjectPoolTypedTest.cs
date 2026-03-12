using System.Collections;
using GameLovers.Services;
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
		public IEnumerator Despawn_DeactivatesGameObject()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			Assert.IsFalse(instance.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
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
		public IEnumerator SpawnWithData_InvokesTypedSpawnHook()
		{
			var instance = _pool.Spawn(42);

			Assert.AreEqual(42, instance.LastSpawnData);
			Assert.AreEqual(1, instance.SpawnCount);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysAllSpawnedInstances()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			_pool.Dispose();

			yield return null;

			Assert.IsTrue(instance1 == null);
			Assert.IsTrue(instance2 == null);
		}
	}
}
