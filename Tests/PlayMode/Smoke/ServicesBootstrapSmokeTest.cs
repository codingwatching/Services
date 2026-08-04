using System.Collections;
using GameLovers.Services;
using GameLovers.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class ServicesBootstrapSmokeTest
	{
		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[Test]
		// ADMIT: bootstrap regression -- the services assembly stops loading, or a service ctor gains a hard dependency.
		// RCR: none required -- Tests/AGENTS.md §1 exempts Smoke/ fixtures from A1/A2; construction-without-throwing
		// is the whole contract here.
		public void AllServices_Instantiate_WithoutException()
		{
			Assert.DoesNotThrow(() => new MessageBrokerService());
			Assert.DoesNotThrow(() => new PoolService());
			Assert.DoesNotThrow(() => new DataService());
			Assert.DoesNotThrow(() => new TimeService());
			Assert.DoesNotThrow(() => new RngService(RngService.CreateRngData(0)));
		}

		[Test]
		public void TickService_CreatesGameObject()
		{
			var service = new TickService();
			var go = GameObject.Find("TickServiceMonoBehaviour");
			Assert.IsNotNull(go);
			service.Dispose();
		}

		[Test]
		public void CoroutineService_CreatesGameObject()
		{
			var service = new CoroutineService();
			var go = GameObject.Find("CoroutineServiceMonoBehaviour");
			Assert.IsNotNull(go);
			service.Dispose();
		}

		[Test]
		// ADMIT: bootstrap regression -- the MainInstaller bind/resolve round trip breaks at assembly load.
		// RCR: none required -- Smoke/ exemption (Tests/AGENTS.md §1). The same round trip is pinned in depth by
		// ServiceLifecycleTest.MainInstaller_BindServices_ResolveAll_Successfully; this copy is the bootstrap canary.
		public void MainInstaller_BindResolve_Works()
		{
			var broker = new MessageBrokerService();
			MainInstaller.Bind<IMessageBrokerService>(broker);
			Assert.AreSame(broker, MainInstaller.Resolve<IMessageBrokerService>());
		}

		[Test]
		// ADMIT: bootstrap regression -- publishing into an empty broker throws at assembly load.
		// RCR: none required -- Smoke/ exemption (Tests/AGENTS.md §1). The no-subscribers early return is duplicated
		// verbatim in Publish and PublishSafe, so no anchor is unique to the overload this test calls.
		public void MessageBroker_PublishWithoutSubscribers_Works()
		{
			var broker = new MessageBrokerService();
			Assert.DoesNotThrow(() => broker.Publish(new SmokeMessage()));
		}

		public struct SmokeMessage : IMessage {}
	}
}
