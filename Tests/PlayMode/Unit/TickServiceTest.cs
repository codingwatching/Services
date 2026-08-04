using System.Collections;
using System.Collections.Generic;
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
		// ADMIT: TickService.Update forwards the elapsed time as `time - LastTickTime`, so a subscriber must never see a
		// negative deltaTime.
		// RCR: TickService.cs Update — flip the subtraction to `LastTickTime - time` → RED (receivedDelta is negative).
		// Also reddens the LateUpdate and RealTime siblings, which read the same sign. 2026-08-02
		public IEnumerator SubscribeOnUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnUpdate(dt => receivedDelta = dt);

			yield return null; // Wait for next frame
			yield return null; // Wait one more to be sure

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		// ADMIT: TickService.Update rate-limits a buffered subscriber by skipping until `LastTickTime + DeltaTime` has
		// elapsed.
		// RCR: TickService.cs Update — drop the `+ tickData.DeltaTime` term from the skip guard → RED (callCount is
		// already ≥1 at the half-interval assertion). 2026-08-02
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
		// ADMIT: TickService.Update carries the modulo remainder back into LastTickTime so a buffered subscriber does not
		// drift a whole interval per tick.
		// RCR: TickService.cs Update — replace the overflow with `-tickData.DeltaTime` so LastTickTime jumps forward
		// instead of back → RED (only one tick lands inside the 2.5-interval wait). 2026-08-02
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
		// ADMIT: TickService.Update special-cases `tickData.DeltaTime == 0` so the overflow calc never evaluates
		// `deltaTime % 0`. Float modulo-by-zero yields NaN (not an exception), and once NaN reaches LastTickTime
		// every later comparison is unordered-false, feeding the subscriber NaN deltaTime on every tick.
		// RCR: TickService.cs Update — drop the zero-guard, leaving `var overFlow = deltaTime % tickData.DeltaTime;`
		// → RED (the NaN assertion below fails from the 2nd received deltaTime on). A callCount-only assertion
		// would NOT redden: NaN comparisons are always false, so ticking never stops — it just carries NaN. 2026-08-01
		public IEnumerator SubscribeOnUpdate_ZeroDeltaTimeWithOverflowToNextTick_TicksEveryFrame()
		{
			var deltaTimes = new List<float>();
			_tickService.SubscribeOnUpdate(dt => deltaTimes.Add(dt), deltaTime: 0f, timeOverflowToNextTick: true);

			yield return null;
			yield return null;
			yield return null;

			Assert.AreEqual(3, deltaTimes.Count);

			foreach (var dt in deltaTimes)
			{
				Assert.IsFalse(float.IsNaN(dt), "Received deltaTime should never be NaN");
			}
		}

		[UnityTest]
		// ADMIT: TickService.Update reads Time.realtimeSinceStartup for a RealTime subscriber, so ticking survives
		// Time.timeScale == 0.
		// RCR: TickService.cs Update — hard-code `var time = Time.time;` → RED (deltaTime is ≤0 under timeScale 0 because
		// LastTickTime was stamped from realtime). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeOnUpdate matches on delegate identity, so a subscriber that unsubscribes itself
		// from inside its own callback is not ticked again.
		// RCR: TickService.cs UnsubscribeOnUpdate — invert the `Action == action` match to `!=` → RED (callCount is 2, not
		// 1). Also reddens Unsubscribe_UmbrellaOverload. 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAll() fans out to all three per-list clears, including the Update list.
		// RCR: TickService.cs UnsubscribeAll() — change the `UnsubscribeAllOnUpdate()` call to a second
		// `UnsubscribeAllOnFixedUpdate()` → RED (both update subscribers still tick). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAll(object) fans out to the per-list subscriber-scoped removals, including the
		// Update list.
		// RCR: TickService.cs UnsubscribeAll(object) — change the `UnsubscribeAllOnUpdate(subscriber)` call to
		// `UnsubscribeAllOnFixedUpdate(subscriber)` → RED (sub1 keeps ticking). 2026-08-02
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
		// ADMIT: TickService.Dispose destroys the DontDestroyOnLoad host GameObject it created in the constructor.
		// RCR: TickService.cs Dispose — drop the `Object.Destroy(_tickObject.gameObject)` call → RED (the host count stays
		// at initialCount + 1 after Dispose). 2026-08-02
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
		// ADMIT: TickService.OnFixedUpdate forwards Time.fixedTime to every fixed-update subscriber, so the received value
		// is never negative.
		// RCR: TickService.cs OnFixedUpdate — forward `-1f` instead of `Time.fixedTime` → RED (receivedDelta is negative).
		// 2026-08-02
		public IEnumerator SubscribeOnFixedUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnFixedUpdate(dt => receivedDelta = dt);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		// ADMIT: TickService's constructor wires the host MonoBehaviour's LateUpdate callback to the late-update list fan-
		// out.
		// RCR: TickService.cs TickService() — wire OnLateUpdate to OnFixedUpdate instead → RED (receivedDelta stays -1).
		// Also reddens the two other late-update tests. 2026-08-02
		public IEnumerator SubscribeOnLateUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnLateUpdate(dt => receivedDelta = dt);

			yield return null;
			yield return null;

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		// ADMIT: TickService.UnsubscribeOnFixedUpdate matches on delegate identity before removing the fixed-update entry.
		// RCR: TickService.cs UnsubscribeOnFixedUpdate — invert the `Action == action` match to `!=` → RED (callCount
		// keeps rising after unsubscribe). Also reddens Unsubscribe_UmbrellaOverload. 2026-08-02
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
		// ADMIT: TickService.UnsubscribeOnLateUpdate matches on delegate identity before removing the late-update entry.
		// RCR: TickService.cs UnsubscribeOnLateUpdate — invert the `Action == action` match to `!=` → RED (callCount keeps
		// rising after unsubscribe). Also reddens Unsubscribe_UmbrellaOverload. 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnUpdate() clears the update list, not one of the sibling lists.
		// RCR: TickService.cs UnsubscribeAllOnUpdate() — clear _onFixedUpdateList instead → RED (both subscribers still
		// tick). Also reddens UnsubscribeAll_RemovesAllSubscribers, which routes through it. 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnUpdate(object) removes the entries whose Subscriber matches, and only those.
		// RCR: TickService.cs UnsubscribeAllOnUpdate(object) — invert the RemoveAll predicate to `!=` → RED (sub1 keeps
		// ticking and sub2 is dropped). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnFixedUpdate() clears the fixed-update list, not one of the sibling lists.
		// RCR: TickService.cs UnsubscribeAllOnFixedUpdate() — clear _onLateUpdateList instead → RED (both fixed-update
		// subscribers still tick). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnFixedUpdate(object) removes the entries whose Subscriber matches, and only
		// those.
		// RCR: TickService.cs UnsubscribeAllOnFixedUpdate(object) — invert the RemoveAll predicate to `!=` → RED (sub1
		// keeps ticking and sub2 is dropped). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnLateUpdate() clears the late-update list, not one of the sibling lists.
		// RCR: TickService.cs UnsubscribeAllOnLateUpdate() — clear _onFixedUpdateList instead → RED (both late-update
		// subscribers still tick). 2026-08-02
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
		// ADMIT: TickService.UnsubscribeAllOnLateUpdate(object) removes the entries whose Subscriber matches, and only
		// those.
		// RCR: TickService.cs UnsubscribeAllOnLateUpdate(object) — invert the RemoveAll predicate to `!=` → RED (sub1
		// keeps ticking and sub2 is dropped). 2026-08-02
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
		// ADMIT: TickService.Unsubscribe(action) is the umbrella that forwards to all three per-list removals, including
		// the fixed-update one.
		// RCR: TickService.cs Unsubscribe — replace the `UnsubscribeOnFixedUpdate(action)` forward with a duplicate
		// `UnsubscribeOnUpdate(action)` → RED (callCount keeps rising from FixedUpdate). 2026-08-02
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
		// ADMIT: TickService does NOT enforce a singleton -- the ctor's `_tickObject != null` guard reads an instance
		// field and is therefore always false, so each construction adds its own TickServiceMonoBehaviour.
		// RCR: TickService.cs -- `private readonly TickServiceMonoBehaviour _tickObject;` -> `private static
		// TickServiceMonoBehaviour _tickObject;` -> RED (production's own ctor guard throws
		// InvalidOperationException "initialized for the second time"). 2026-08-04
		public void MultipleInstances_CreateMultipleGameObjects()
		{
			var service1 = new TickService();
			var service2 = new TickService();
			
			var objects = Object.FindObjectsByType<TickServiceMonoBehaviour>();
			Assert.GreaterOrEqual(objects.Length, 2);
			
			service1.Dispose();
			service2.Dispose();
		}
	}
}
