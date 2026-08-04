using System.Collections;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class ServiceLifecycleTest
	{
		public struct TestMessage : IMessage {}

		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[UnityTest]
		// ADMIT: MessageBrokerService.Publish invokes each stored delegate, which is what turns a TickService callback
		// into a delivered message.
		// RCR: MessageBrokerService.cs Publish — drop the `action(message)` invocation → RED (messageReceived stays false
		// after two frames). 2026-08-02
		public IEnumerator TickService_WithMessageBroker_PublishesOnTick()
		{
			var tickService = new TickService();
			var broker = new MessageBrokerService();
			var messageReceived = false;

			broker.Subscribe<TestMessage>(m => messageReceived = true);
			tickService.SubscribeOnUpdate(dt => broker.Publish(new TestMessage()));

			yield return null;
			yield return null;

			Assert.IsTrue(messageReceived);
			
			tickService.Dispose();
		}

		[UnityTest]
		// ADMIT: PoolService.Spawn<T> resolves the registered pool and returns its spawned instance.
		// RCR: PoolService.cs Spawn<T> — return null instead of delegating to the pool → RED (Assert.IsNotNull(instance)
		// fails). 2026-08-02
		public IEnumerator PoolService_WithGameObjectPool_FullLifecycle()
		{
			var poolService = new PoolService();
			var sample = new GameObject("Sample");
			var pool = new GameObjectPool(0, sample);
			
			poolService.AddPool(pool);
			
			var instance = poolService.Spawn<GameObject>();
			Assert.IsNotNull(instance);
			Assert.IsTrue(instance.activeSelf);
			
			poolService.Despawn(instance);
			Assert.IsFalse(instance.activeSelf);
			
			Object.Destroy(sample);
			pool.Dispose();
			yield return null;
		}

		[Test]
		// ADMIT: MainInstaller.Resolve<T> delegates to the private Installer so bound services come back out.
		// RCR: MainInstaller.cs Resolve<T> — return default(T) instead of delegating → RED (all three IsNotNull assertions
		// fail). Also reddens the PlayMode smoke bind/resolve test. 2026-08-02
		public void MainInstaller_BindServices_ResolveAll_Successfully()
		{
			MainInstaller.Bind<ITickService>(new TickService());
			MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
			MainInstaller.Bind<IPoolService>(new PoolService());
			
			Assert.IsNotNull(MainInstaller.Resolve<ITickService>());
			Assert.IsNotNull(MainInstaller.Resolve<IMessageBrokerService>());
			Assert.IsNotNull(MainInstaller.Resolve<IPoolService>());
			
			MainInstaller.CleanDispose<ITickService>();
			MainInstaller.Clean<IMessageBrokerService>();
			MainInstaller.Clean<IPoolService>();
		}
	}
}
