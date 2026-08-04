using System;
using System.Collections.Generic;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class PoolServiceTest
	{
		private PoolService _poolService;
		private IObjectPool<IMockPoolableEntity> _pool;

		public interface IMockPoolableEntity : IPoolEntitySpawn, IPoolEntityDespawn { }
		public class MockPoolableEntity : IMockPoolableEntity
		{
			public void OnSpawn() {}
			public void OnDespawn() {}
		}

		public interface IMockDataEntity : IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int> { }
		public class MockDataEntity : IMockDataEntity
		{
			public int SpawnData;
			public void OnSpawn() {}
			public void OnDespawn() {}
			public void OnSpawn(int data) => SpawnData = data;
		}

		// Hand-written fake — NSubstitute can't proxy IObjectPool<T> with self-referential
		// generic args on Mono. See Tests/AGENTS.md §4.
		private class FakeObjectPool<T> : IObjectPool<T> where T : class
		{
			public int DisposeCount;

			public T SampleEntity => null;
			public IReadOnlyList<T> SpawnedReadOnly => System.Array.Empty<T>();

			public void Dispose() { DisposeCount++; }
			public void Dispose(bool disposeSampleEntity) { DisposeCount++; }

			public bool IsSpawned(System.Func<T, bool> conditionCheck) => false;
			public void Reset(uint initSize, T sampleEntity) { }
			public T Spawn() => null;
			public T Spawn<TData>(TData data) => null;
			public bool Despawn(bool onlyFirst, System.Func<T, bool> entityGetter) => false;
			public bool Despawn(T entity) => false;
			public List<T> Clear() => new List<T>();
			public void DespawnAll() { }
		}

		[SetUp]
		public void Init()
		{
			_poolService = new PoolService();
			_pool = new ObjectPool<IMockPoolableEntity>(0, () => new MockPoolableEntity());
			
			_poolService.AddPool(_pool);
		}

		[TearDown]
		public void Dispose()
		{
			_poolService.Dispose();
		}

		[Test]
		// ADMIT: PoolService.TryGetPool casts the stored IObjectPool to IObjectPool<T> and outs it.
		// RCR: PoolService.cs TryGetPool — out null instead of the cast pool → RED (AreEqual(_pool, pool) fails). Also
		// reddens GetPool_Successfully. 2026-08-02
		public void TryGetPool_Successfully()
		{
			Assert.True(_poolService.TryGetPool<IMockPoolableEntity>(out var pool));
			Assert.AreEqual(_pool, pool);
		}

		[Test]
		// ADMIT: PoolService.GetPool returns the pool TryGetPool resolved for the requested type.
		// RCR: PoolService.cs GetPool — return null after the guard → RED (AreEqual(_pool, …) fails). Other Spawn/Despawn
		// tests fail by NRE under this mutation. 2026-08-02
		public void GetPool_Successfully()
		{
			Assert.AreEqual(_pool, _poolService.GetPool<IMockPoolableEntity>());
		}

		[Test]
		public void AddPool_Successfully()
		{
			Assert.True(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		// ADMIT: PoolService.AddPool uses Dictionary.Add, so registering a second pool for the same type throws instead of
		// silently replacing.
		// RCR: PoolService.cs AddPool — switch to the indexer assignment → RED (no ArgumentException is thrown).
		// 2026-08-02
		public void AddPool_SameType_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _poolService.AddPool(_pool));
		}

		[Test]
		// ADMIT: PoolService.Spawn<T> delegates to the registered pool and returns its instance.
		// RCR: PoolService.cs Spawn<T> — return null instead of delegating → RED (Assert.IsNotNull(entity) fails).
		// 2026-08-02
		public void Spawn_Successfully()
		{
			var entity = _poolService.Spawn<IMockPoolableEntity>();
			
			Assert.IsNotNull(entity);
			Assert.IsInstanceOf<MockPoolableEntity>(entity);
		}

		[Test]
		// ADMIT: PoolService.GetPool throws ArgumentException when no pool is registered for the requested type.
		// RCR: PoolService.cs GetPool — return null instead of throwing → RED (NullReferenceException, not
		// ArgumentException). Also reddens Despawn_NotAddedPool and RemovePool_Successfully. 2026-08-02
		public void Spawn_NotAddedPool_ThrowsException()
		{
			_poolService = new PoolService();
			
			Assert.Throws<ArgumentException>(() => _poolService.Spawn<IMockPoolableEntity>());
		}

		[Test]
		public void Despawn_Successfully()
		{
			var entity = _poolService.Spawn<IMockPoolableEntity>();
			
			Assert.DoesNotThrow(() => _poolService.Despawn(entity));
		}

		[Test]
		// ADMIT: PoolService.Despawn<T> routes through GetPool, so despawning into an unregistered type surfaces GetPool's
		// ArgumentException.
		// RCR: PoolService.cs Despawn<T> — return false without resolving the pool → RED (no ArgumentException is thrown).
		// 2026-08-02
		public void Despawn_NotAddedPool_ThrowsException()
		{
			var entity = new MockPoolableEntity();
			
			_poolService = new PoolService();
			
			Assert.Throws<ArgumentException>(() => _poolService.Despawn(entity));
		}

		[Test]
		public void DespawnAll_Successfully()
		{
			_poolService.Spawn<IMockPoolableEntity>();
			_poolService.DespawnAll<IMockPoolableEntity>();
			
			Assert.DoesNotThrow(() => _poolService.DespawnAll<IMockPoolableEntity>());
		}

		[Test]
		// ADMIT: PoolService.RemovePool deletes the registry entry so a later GetPool throws.
		// RCR: PoolService.cs RemovePool — drop the dictionary removal → RED (GetPool still resolves, no
		// ArgumentException). Also reddens Dispose_RemovesAndDisposesPool. 2026-08-02
		public void RemovePool_Successfully()
		{
			_poolService.RemovePool<IMockPoolableEntity>();

			Assert.Throws<ArgumentException>(() => _poolService.GetPool<IMockPoolableEntity>());
		}

		[Test]
		public void RemovePool_NotAdded_DoesNothing()
		{
			_poolService = new PoolService();
			
			Assert.DoesNotThrow(() => _poolService.RemovePool<IMockPoolableEntity>());
		}

		[Test]
		// ADMIT: PoolService.Spawn<T, TData> forwards the payload to the pool's data-aware Spawn overload.
		// RCR: PoolService.cs Spawn<T, TData> — call the parameterless Spawn instead → RED (SpawnData stays 0, not 42).
		// 2026-08-02
		public void SpawnWithData_Successfully()
		{
			var dataPool = new ObjectPool<IMockDataEntity>(0, () => new MockDataEntity());
			_poolService.AddPool(dataPool);

			var entity = _poolService.Spawn<IMockDataEntity, int>(42);

			Assert.IsNotNull(entity);
			Assert.AreEqual(42, ((MockDataEntity)entity).SpawnData);
		}

		[Test]
		// ADMIT: PoolService.Clear returns a snapshot of the registry it is about to empty.
		// RCR: PoolService.cs Clear — return an empty dictionary instead of a copy of _pools → RED (cleared.Count is 0,
		// not 1). 2026-08-02
		public void Clear_ReturnsAllPools()
		{
			IDictionary<Type, IObjectPool> cleared = _poolService.Clear();

			Assert.AreEqual(1, cleared.Count);
			Assert.IsTrue(cleared.ContainsKey(typeof(IMockPoolableEntity)));
			Assert.IsFalse(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		// ADMIT: PoolService.Dispose<T>(bool) unregisters the pool after disposing it.
		// RCR: PoolService.cs Dispose<T>(bool) — drop the `RemovePool<T>()` call → RED (TryGetPool still resolves the
		// disposed pool). 2026-08-02
		public void Dispose_RemovesAndDisposesPool()
		{
			_poolService.Dispose<IMockPoolableEntity>(disposeSampleEntity: false);

			Assert.IsFalse(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		// ADMIT: PoolService.Dispose() disposes every registered pool before clearing the registry.
		// RCR: PoolService.cs Dispose() — drop the per-pool `Dispose()` call → RED (both fakes report DisposeCount 0).
		// 2026-08-02
		public void Dispose_DisposesAllRegisteredPools()
		{
			var fakeA = new FakeObjectPool<IMockPoolableEntity>();
			var fakeB = new FakeObjectPool<IMockDataEntity>();

			var service = new PoolService();
			service.AddPool<IMockPoolableEntity>(fakeA);
			service.AddPool<IMockDataEntity>(fakeB);

			service.Dispose();

			Assert.AreEqual(1, fakeA.DisposeCount);
			Assert.AreEqual(1, fakeB.DisposeCount);
			Assert.IsFalse(service.TryGetPool<IMockPoolableEntity>(out _));
			Assert.IsFalse(service.TryGetPool<IMockDataEntity>(out _));
		}
	}
}
