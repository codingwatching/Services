using GameLovers.Services;
using GameLovers.Services.Pooling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	/// <summary>
	/// Performance tests for ObjectPool.
	/// Uses PrebuildSetup and OneTimeSetUp to ensure performance test metadata is initialized before tests run.
	/// </summary>
	[TestFixture]
	[Category("Performance")]
	[PrebuildSetup(typeof(PerformanceTestSetup))]
	public class ObjectPoolPerformanceTest
	{
		public class MockEntity
		{
		}

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			PerformanceTestSetup.InitializePerformanceTestMetadata();
		}

		[Test, Performance]
		public void ObjectPool_SpawnDespawn_1000Cycles()
		{
			var pool = new ObjectPool<MockEntity>(1000, () => new MockEntity());
			var entities = new MockEntity[1000];

			Measure.Method(() =>
				{
					for (var i = 0; i < 1000; i++) entities[i] = pool.Spawn();
					for (var i = 0; i < 1000; i++) pool.Despawn(entities[i]);
				})
				.WarmupCount(5)
				.MeasurementCount(20)
				.Run();
		}
	}
}
