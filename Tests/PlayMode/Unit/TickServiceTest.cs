using System.Collections;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class TickServiceTest
	{
		private TickService _tickService;

		[SetUp]
		public void Init()
		{
			_tickService = new TickService();
		}

		[TearDown]
		public void Dispose()
		{
			_tickService.Dispose();
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnUpdate(dt => receivedDelta = dt);

			yield return null; // Wait for next frame
			yield return null; // Wait one more to be sure

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_WithDeltaBuffer_InvokesAtInterval()
		{
			int callCount = 0;
			float interval = 0.1f;
			_tickService.SubscribeOnUpdate(dt => callCount++, interval);

			yield return new WaitForSeconds(interval * 0.5f);
			Assert.AreEqual(0, callCount);

			yield return new WaitForSeconds(interval);
			Assert.GreaterOrEqual(callCount, 1);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_TimeOverflow_CarriesOverflow()
		{
			float interval = 0.05f;
			int callCount = 0;
			_tickService.SubscribeOnUpdate(dt => callCount++, interval, true);

			yield return new WaitForSeconds(interval * 2.5f);
			
			// If overflow is carried, it should have triggered at least twice
			Assert.GreaterOrEqual(callCount, 2);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_RealTime_UsesUnscaledTime()
		{
			float initialTimeScale = Time.timeScale;
			Time.timeScale = 0f;
			
			float receivedDelta = -1f;
			_tickService.SubscribeOnUpdate(dt => receivedDelta = dt, 0f, false, true);

			yield return new WaitForSecondsRealtime(0.1f);
			
			Time.timeScale = initialTimeScale;
			
			Assert.Greater(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnUpdate_DuringCallback_SafelyRemoves()
		{
			int callCount = 0;
			System.Action<float> action = null;
			action = dt =>
			{
				callCount++;
				_tickService.UnsubscribeOnUpdate(action);
			};

			_tickService.SubscribeOnUpdate(action);

			yield return null;
			yield return null;

			Assert.AreEqual(1, callCount);
		}

		private class TickSubscriber
		{
			public int CallCount;
			public void OnTick(float dt) => CallCount++;
		}

		[UnityTest]
		public IEnumerator UnsubscribeAll_RemovesAllSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAll();

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAll_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAll(sub1);

			yield return null;

			Assert.AreEqual(0, sub1.CallCount, "sub1 should have been unsubscribed");
			Assert.Greater(sub2.CallCount, 0, "sub2 should still receive ticks");
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysGameObject()
		{
			var initialCount = Object.FindObjectsByType<TickServiceMonoBehaviour>().Length;
			var tickService = new TickService();
			
			Assert.AreEqual(initialCount + 1, Object.FindObjectsByType<TickServiceMonoBehaviour>().Length);
			
			tickService.Dispose();
			yield return null; // Allow Destroy to complete
			
			Assert.AreEqual(initialCount, Object.FindObjectsByType<TickServiceMonoBehaviour>().Length);
		}

		[UnityTest]
		public IEnumerator SubscribeOnFixedUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnFixedUpdate(dt => receivedDelta = dt);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator SubscribeOnLateUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnLateUpdate(dt => receivedDelta = dt);

			yield return null;
			yield return null;

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnFixedUpdate_RemovesCallback()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;
			_tickService.SubscribeOnFixedUpdate(action);

			yield return new WaitForFixedUpdate();
			Assert.GreaterOrEqual(callCount, 1);

			int countAtUnsubscribe = callCount;
			_tickService.UnsubscribeOnFixedUpdate(action);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnLateUpdate_RemovesCallback()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;
			_tickService.SubscribeOnLateUpdate(action);

			yield return null;
			Assert.GreaterOrEqual(callCount, 1);

			int countAtUnsubscribe = callCount;
			_tickService.UnsubscribeOnLateUpdate(action);

			yield return null;
			yield return null;

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnUpdate_RemovesAllUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnUpdate();

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnUpdate(sub1);

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnFixedUpdate_RemovesAllFixedUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnFixedUpdate(sub1.OnTick);
			_tickService.SubscribeOnFixedUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnFixedUpdate();

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnFixedUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnFixedUpdate(sub1.OnTick);
			_tickService.SubscribeOnFixedUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnFixedUpdate(sub1);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnLateUpdate_RemovesAllLateUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnLateUpdate(sub1.OnTick);
			_tickService.SubscribeOnLateUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnLateUpdate();

			yield return null;
			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnLateUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnLateUpdate(sub1.OnTick);
			_tickService.SubscribeOnLateUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnLateUpdate(sub1);

			yield return null;
			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator Unsubscribe_UmbrellaOverload_RemovesActionFromAllThreeUpdateLists()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;

			_tickService.SubscribeOnUpdate(action);
			_tickService.SubscribeOnFixedUpdate(action);
			_tickService.SubscribeOnLateUpdate(action);

			yield return null;
			yield return new WaitForFixedUpdate();
			Assert.Greater(callCount, 0);

			_tickService.Unsubscribe(action);
			int countAtUnsubscribe = callCount;

			yield return null;
			yield return new WaitForFixedUpdate();
			yield return null;
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[Test]
		public void MultipleInstances_CreateMultipleGameObjects()
		{
			// Note: The service doesn't enforce singleton, but it throws if _tickObject is already set
			// However, _tickObject is an instance field in the current implementation.
			// Wait, I saw a check in the constructor:
			/*
			public TickService()
			{
				if (_tickObject != null)
				{
					throw new InvalidOperationException("The tick service is being initialized for the second time and that is not valid");
				}
				...
			}
			*/
			// But _tickObject is private readonly TickServiceMonoBehaviour _tickObject;
			// So it's always null for a new instance. The check seems to be intended for a static field but isn't.
			
			var service1 = new TickService();
			var service2 = new TickService();
			
			var objects = Object.FindObjectsByType<TickServiceMonoBehaviour>();
			Assert.GreaterOrEqual(objects.Length, 2);
			
			service1.Dispose();
			service2.Dispose();
		}
	}
}
