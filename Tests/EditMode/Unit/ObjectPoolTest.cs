using System;
using System.Collections.Generic;
using System.Xml.Linq;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class ObjectPoolTest
	{
		private ObjectPool<IMockEntity> _pool;
		private IMockEntity _mockEntity;
		private uint _initialSize = 5;

		public interface IMockEntity : IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntityObject<IMockEntity>, IPoolEntitySpawn<object> { }
		public class MockEntity : IMockEntity
		{
			private IObjectPool<IMockEntity> _pool;

			public int InitCount { get; private set; }
			public IObjectPool<IMockEntity> LastInitPool => _pool;

			public void Init(IObjectPool<IMockEntity> pool)
			{
				_pool = pool;
				InitCount++;
			}

			public bool Despawn() => _pool.Despawn(this);
			public void OnDespawn()	{}

			public void OnSpawn() {}
			public void OnSpawn(object data) {}
		}

		[SetUp]
		public void Init()
		{
			_mockEntity = Substitute.For<IMockEntity>();
			_pool = new ObjectPool<IMockEntity>(_initialSize, () => _mockEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Spawn() runs the IPoolEntitySpawn lifecycle hook on the entity it hands out.
		// RCR: ObjectPool.cs Spawn() — drop the `CallOnSpawned(entity)` call → RED (Received().OnSpawn() is never
		// satisfied). Also reddens Spawn_ZeroInitialSize_Successfully. 2026-08-02
		public void Spawn_Successfully()
		{
			var newEntity = _pool.Spawn();
			
			newEntity.Received().OnSpawn();
			
			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Spawn<TData> runs the typed IPoolEntitySpawn<TData> hook in addition to the untyped
		// one.
		// RCR: ObjectPool.cs Spawn<TData> — drop the `CallOnSpawned(entity, data)` call → RED (Received().OnSpawn(obj) is
		// never satisfied). 2026-08-02
		public void Spawn_WithData_Successfully()
		{
			var obj = new object();
			var newEntity = _pool.Spawn(obj);

			newEntity.Received().OnSpawn(obj);

			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.SpawnEntity instantiates a fresh entity when the free stack is empty instead of popping
		// it.
		// RCR: ObjectPool.cs SpawnEntity — pop unconditionally → RED (InvalidOperationException 'Stack empty' on a zero-
		// init pool). Also reddens the other zero-init fixtures in this file. 2026-08-02
		public void Spawn_ZeroInitialSize_Successfully()
		{
			var pool = new ObjectPool<IMockEntity>(0, () => _mockEntity);
			var newEntity = pool.Spawn();

			newEntity.Received().OnSpawn();

			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Despawn runs the IPoolEntityDespawn hook on a successfully returned entity.
		// RCR: ObjectPool.cs Despawn(T) — drop the `CallOnDespawned(entity)` call → RED (Received().OnDespawn() is never
		// satisfied). Also reddens DespawnAll_Successfully. 2026-08-02
		public void Despawn_Successfully()
		{
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(_mockEntity));
			_mockEntity.Received().OnDespawn();
		}

		[Test]
		public void EntityDespawn_Successfully()
		{
			// Using a real ObjectPool<IMockEntity> instead of Substitute.For<IObjectPool<IMockEntity>>()
			// because NSubstitute + Castle DynamicProxy crashes during proxy generation on Unity's Mono
			// runtime when the generic argument is a self-referential interface
			// (IMockEntity : IPoolEntityObject<IMockEntity>) — ILGenerator.DeclareLocal receives a null
			// localType. The real pool exercises the same MockEntity.Despawn -> pool.Despawn(this)
			// contract, and SpawnedReadOnly.Count confirms the routing via observable state.
			MockEntity sharedEntity = null;
			var pool = new ObjectPool<IMockEntity>(1, () => sharedEntity ??= new MockEntity());
			var entity = pool.Spawn();

			Assert.AreSame(sharedEntity, entity);
			Assert.AreEqual(1, pool.SpawnedReadOnly.Count);
			Assert.IsTrue(sharedEntity.Despawn());
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.CallInstantiator is the only site that calls IPoolEntityObject<T>.Init(pool),
		// so every instance the pool produces must be told which pool owns it.
		// RCR: ObjectPool.cs CallInstantiator — delete `poolEntity?.Init(this);` → RED (InitCount stays 0 instead
		// of 2, LastInitPool stays null). 2026-08-01
		public void Spawn_OnPoolEntityObject_CallsInitWithOwningPoolEveryTime()
		{
			MockEntity sharedEntity = null;
			var pool = new ObjectPool<IMockEntity>(0, () => sharedEntity ??= new MockEntity());

			pool.Spawn();
			pool.Spawn();

			Assert.AreEqual(2, sharedEntity.InitCount);
			Assert.AreSame(pool, sharedEntity.LastInitPool);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Despawn reports false for an entity that was never spawned from this pool.
		// RCR: ObjectPool.cs Despawn(T) — return true from the reject branch → RED (Assert.IsFalse fails). Note: deleting
		// the `SpawnedEntities.Remove` term instead hangs Despawn(bool, Func). 2026-08-02
		public void Despawn_NotSpawnedObject_ReturnsFalse()
		{
			Assert.IsFalse(_pool.Despawn(_mockEntity));
			_mockEntity.DidNotReceive().OnDespawn();
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.DespawnAll actually despawns each tracked entity rather than just walking the list.
		// RCR: ObjectPool.cs DespawnAll — drop the `Despawn(SpawnedEntities[i])` call → RED (neither entity receives
		// OnDespawn). 2026-08-02
		public void DespawnAll_Successfully()
		{
			var newEntity1 = _pool.Spawn();
			var newEntity2 = _pool.Spawn();
			
			_pool.DespawnAll();

			newEntity1.Received().OnDespawn();
			newEntity2.Received().OnDespawn();
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Clear returns the pre-instantiated free-stack entities as well as the spawned ones.
		// RCR: ObjectPool.cs Clear — drop the `ret.AddRange(_stack)` call → RED (0 returned instead of the 5 pre-
		// instantiated entities). 2026-08-02
		public void Clear_Successfully()
		{
			var clearedEntities = _pool.Clear();

			Assert.AreEqual(_initialSize, clearedEntities.Count);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>'s constructor stores the sample entity that SampleEntity exposes.
		// RCR: ObjectPool.cs ObjectPoolBase<T>(uint, T, Func) — null the `_sampleEntity` assignment → RED (SampleEntity is
		// null, not the mock). 2026-08-02
		public void SampleEntity_ReturnsSampleEntity()
		{
			Assert.AreSame(_mockEntity, _pool.SampleEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.SpawnedReadOnly exposes the live SpawnedEntities backing list, not a detached copy.
		// RCR: ObjectPool.cs SpawnedReadOnly — return a fresh empty list → RED (count 0 and no element to compare). Also
		// reddens the other SpawnedReadOnly-count assertions in this fixture. 2026-08-02
		public void SpawnedReadOnly_ReturnsSpawnedEntities()
		{
			var entity = _pool.Spawn();

			var spawned = _pool.SpawnedReadOnly;

			Assert.AreEqual(1, spawned.Count);
			Assert.AreSame(entity, spawned[0]);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.IsSpawned returns true for the first spawned entity satisfying the predicate and false
		// otherwise.
		// RCR: ObjectPool.cs IsSpawned — invert the predicate test → RED (the matching probe returns false and the always-
		// false probe returns true). 2026-08-02
		public void IsSpawned_ReturnsTrueWhenMatch()
		{
			var entity = _pool.Spawn();

			Assert.IsTrue(_pool.IsSpawned(e => e == entity));
			Assert.IsFalse(_pool.IsSpawned(e => false));
		}

		[Test]
		public void Despawn_WithCondition_FirstOnly_Successfully()
		{
			var entity = _pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: true, e => e == entity));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Despawn(bool, Func) reports false when the predicate matched nothing.
		// RCR: ObjectPool.cs Despawn(bool, Func) — seed the result accumulator to true → RED (Assert.IsFalse fails while
		// the entity correctly survives). 2026-08-02
		public void Despawn_WithCondition_NoMatch_ReturnsFalse()
		{
			_pool.Spawn();

			Assert.IsFalse(_pool.Despawn(onlyFirst: true, e => false));
			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_WithCondition_AllMatching_DespawnsAll()
		{
			_pool.Spawn();
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Despawn(bool, Func) steps the index back after a removal so adjacent distinct matches
		// are not skipped.
		// RCR: ObjectPool.cs Despawn(bool, Func) — delete the `i--` step-back → RED (only the first of two distinct
		// entities is despawned). Also reddens Despawn_WithCondition_AllMatching_DespawnsAll. 2026-08-02
		public void Despawn_WithCondition_DistinctMatchingEntities_AllDespawn()
		{
			// Regression: Despawn_WithCondition_AllMatching_DespawnsAll spawns the same _mockEntity
			// twice (the SetUp factory returns a single instance), so SpawnedEntities.Remove matches
			// by reference equality on duplicates. This test uses DISTINCT entities to confirm the
			// iterate-while-mutating fix in ObjectPoolBase<T>.Despawn(bool, Func) also holds when
			// each matching element is a separate reference.
			var pool = new ObjectPool<IMockEntity>(0, () => Substitute.For<IMockEntity>());
			var first = pool.Spawn();
			var second = pool.Spawn();

			Assert.AreNotSame(first, second);
			Assert.IsTrue(pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Despawn(bool, Func) skips predicate-rejected entities so non-matching neighbours
		// survive the step-back walk.
		// RCR: ObjectPool.cs Despawn(bool, Func) — delete the predicate skip → RED (the keeper is despawned too, count 0).
		// Also reddens Despawn_WithCondition_NoMatch_ReturnsFalse. 2026-08-02
		public void Despawn_WithCondition_PartialMatch_NonMatchingSurvives()
		{
			// Confirms the iteration step-back after a successful despawn doesn't spuriously remove
			// non-matching neighbours when only a subset of the spawned set matches the predicate.
			var pool = new ObjectPool<IMockEntity>(0, () => Substitute.For<IMockEntity>());
			var target = pool.Spawn();
			var keeper = pool.Spawn();

			Assert.IsTrue(pool.Despawn(onlyFirst: false, e => e == target));
			Assert.AreEqual(1, pool.SpawnedReadOnly.Count);
			Assert.AreSame(keeper, pool.SpawnedReadOnly[0]);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.Reset re-seeds _sampleEntity with the new sample after disposing the old contents.
		// RCR: ObjectPool.cs Reset — drop the `_sampleEntity = sampleEntity` assignment → RED (SampleEntity still points
		// at the original mock). 2026-08-02
		public void Reset_ClearsAndReinitializes()
		{
			_pool.Spawn();
			var newSample = Substitute.For<IMockEntity>();
			uint newSize = 3;

			_pool.Reset(newSize, newSample);

			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(newSample, _pool.SampleEntity);
		}

		[Test]
		// ADMIT: ObjectPoolBase<T>.SpawnEntity reuses the pre-instantiated free stack, so the Func-only ctor's factory is
		// invoked exactly initSize + 1 times.
		// RCR: ObjectPool.cs SpawnEntity — always instantiate instead of popping → RED (invocations is 7, not 4, after
		// three spawns). 2026-08-02
		public void ObjectPool_FuncOnlyCtor_UsesProvidedFactory()
		{
			var invocations = 0;
			IMockEntity Factory()
			{
				invocations++;
				return Substitute.For<IMockEntity>();
			}

			const uint initSize = 3;
			var pool = new ObjectPool<IMockEntity>(initSize, Factory);

			Assert.AreEqual((int)initSize + 1, invocations);

			pool.Spawn();
			pool.Spawn();
			pool.Spawn();

			Assert.AreEqual((int)initSize + 1, invocations);
			Assert.AreEqual(3, pool.SpawnedReadOnly.Count);
		}
	}
}