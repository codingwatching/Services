using System.Collections;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class CoroutineServiceTest
	{
		private CoroutineService _coroutineService;
		private int _testValue;

		private IEnumerator TestCoroutine(int value)
		{
			yield return null;

			_testValue = value;
		}

		[SetUp]
		public void Init()
		{
			_coroutineService = new CoroutineService();
			_testValue = 0;
		}

		[TearDown]
		public void Dispose()
		{
			_coroutineService.Dispose();
		}
		
		[UnityTest]
		// ADMIT: CoroutineService.StartCoroutine hands the routine to the host MonoBehaviour and returns its Coroutine
		// handle.
		// RCR: CoroutineService.cs StartCoroutine — return null without starting the routine → RED (_testValue stays 0).
		// 2026-08-02
		public IEnumerator StartCoroutine_Successfully()
		{
			const int testValue1 = 5;

			yield return _coroutineService.StartCoroutine(TestCoroutine(testValue1));
			
			Assert.AreEqual(testValue1, _testValue); 
		}
		
		[UnityTest]
		// ADMIT: CoroutineService.InternalCoroutine signals the AsyncCoroutine wrapper after the wrapped routine finishes.
		// RCR: CoroutineService.cs InternalCoroutine — drop the `completed.Completed()` call → RED (IsCompleted false,
		// OnComplete never fires). Also reddens the other natural-completion tests. 2026-08-02
		public IEnumerator StartAsyncCoroutine_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);

			yield return asyncCoroutine.Coroutine;
			
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(testValue1, _testValue); 
			Assert.AreEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		// ADMIT: CoroutineService.StartAsyncCoroutine<T> seeds the AsyncCoroutine<T> payload with the caller's data.
		// RCR: CoroutineService.cs StartAsyncCoroutine<T> — construct the wrapper with `default` instead of `data` → RED
		// (the Action<T> callback receives 0, not 10). 2026-08-02
		public IEnumerator StartAsyncCoroutine_WithData_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			var asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1), testValue2);
			asyncCoroutine.OnComplete(newValue => testCompleted = newValue);

			yield return asyncCoroutine.Coroutine;
			
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(testValue1, _testValue); 
			Assert.AreEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		// ADMIT: CoroutineServiceMonoBehaviour.ExternalStopCoroutine is the only path that reaches Unity's
		// MonoBehaviour.StopCoroutine for a service-owned handle.
		// RCR: CoroutineService.cs ExternalStopCoroutine — make the body a no-op → RED (the routine completes and
		// _testValue becomes 5). Also reddens StopAsyncCoroutine_Successfully. 2026-08-02
		public IEnumerator StopCoroutine_Successfully()
		{
			const int testValue1 = 5;

			var coroutine = _coroutineService.StartCoroutine(TestCoroutine(testValue1));
			_coroutineService.StopCoroutine(coroutine);
			
			Assert.AreNotEqual(testValue1, _testValue); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.AreNotEqual(testValue1, _testValue); 
		}
		
		[UnityTest]
		// ADMIT: CoroutineService.StopCoroutine forwards a live handle to the host after its guard passes, and stopping
		// the raw handle leaves the async wrapper un-completed.
		// RCR: CoroutineService.cs StopCoroutine — drop the forward to ExternalStopCoroutine → RED (routine finishes:
		// IsCompleted true and testCompleted 10). Overlaps StopCoroutine_Successfully. 2026-08-02
		public IEnumerator StopAsyncCoroutine_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);
			
			_coroutineService.StopCoroutine(asyncCoroutine.Coroutine);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		// ADMIT: CoroutineService.StopAllCoroutines only bails out when the host is gone; with a live host it must reach
		// the host's StopAllCoroutines.
		// RCR: CoroutineService.cs StopAllCoroutines — invert the guard to `if (_serviceObject != null)` so it early-
		// returns while alive → RED (both routines run to completion). 2026-08-02
		public IEnumerator StopAllCoroutines_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			const int testValue3 = 20;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);
			_coroutineService.StartCoroutine(TestCoroutine(testValue3));
			
			_coroutineService.StopAllCoroutines();
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
			Assert.AreNotEqual(testValue3, _testValue); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
			Assert.AreNotEqual(testValue3, _testValue); 
		}

		[UnityTest]
		// ADMIT: CoroutineService.StopCoroutine guards a null `coroutine` handle before forwarding to
		// _serviceObject.ExternalStopCoroutine.
		// RCR: CoroutineService.cs StopCoroutine — remove the `coroutine == null ||` term from the guard → RED
		// (falls through to MonoBehaviour.StopCoroutine(null), which throws). 2026-08-01
		public IEnumerator StopCoroutine_NullCoroutine_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => _coroutineService.StopCoroutine(null));

			yield return null;
		}

		[UnityTest]
		// ADMIT: CoroutineService.StopCoroutine also guards the host _serviceObject being Unity fake-null (native
		// object destroyed while the C# reference survives) — distinct from Dispose(), which assigns a real null.
		// This test destroys the host GameObject directly to reproduce that fake-null path.
		// RCR: CoroutineService.cs StopCoroutine — remove the `_serviceObject == null ||` term from the guard →
		// RED (MissingReferenceException from `_serviceObject.gameObject`). 2026-08-01
		public IEnumerator StopCoroutine_AfterServiceObjectDestroyed_DoesNotThrowMissingReference()
		{
			var coroutine = _coroutineService.StartCoroutine(TestCoroutine(5));
			var host = Object.FindObjectsByType<CoroutineServiceMonoBehaviour>()[0];

			Object.Destroy(host.gameObject);

			yield return null; // Allow the native destruction to be reflected on the host reference

			Assert.DoesNotThrow(() => _coroutineService.StopCoroutine(coroutine));
		}

		[UnityTest]
		// ADMIT: CoroutineService.Dispose destroys the DontDestroyOnLoad host GameObject it created in the constructor.
		// RCR: CoroutineService.cs Dispose — drop the `Object.Destroy(_serviceObject.gameObject)` call → RED (host count
		// stays at initialCount + 1). 2026-08-02
		public IEnumerator Dispose_DestroysHostGameObject()
		{
			var initialCount = Object.FindObjectsByType<CoroutineServiceMonoBehaviour>().Length;
			var service = new CoroutineService();

			Assert.AreEqual(
				initialCount + 1,
				Object.FindObjectsByType<CoroutineServiceMonoBehaviour>().Length);

			service.Dispose();
			yield return null;

			Assert.AreEqual(
				initialCount,
				Object.FindObjectsByType<CoroutineServiceMonoBehaviour>().Length);
		}

		[UnityTest]
		// ADMIT: CoroutineService.Dispose guards on the already-nulled _serviceObject so a second Dispose is a no-op.
		// RCR: CoroutineService.cs Dispose — delete the `if(_serviceObject == null) return;` guard → RED (the second
		// Dispose throws NullReferenceException and DoesNotThrow fails). 2026-08-02
		public IEnumerator Dispose_CalledTwice_DoesNotThrow()
		{
			var service = new CoroutineService();
			service.Dispose();
			yield return null;

			Assert.DoesNotThrow(() => service.Dispose());
		}

		[UnityTest]
		// ADMIT: CoroutineService.StartDelayCall registers the caller's Action as the wrapper's completion callback before
		// starting the delay routine.
		// RCR: CoroutineService.cs StartDelayCall — register an empty lambda instead of `call` → RED (`called` stays false
		// after the delay elapses). 2026-08-02
		public IEnumerator StartDelayCall_Successfully()
		{
			bool called = false;
			_coroutineService.StartDelayCall(() => called = true, delay: 0.05f);

			Assert.IsFalse(called);

			yield return new WaitForSeconds(0.2f);

			Assert.IsTrue(called);
		}

		[UnityTest]
		// ADMIT: CoroutineService.StartDelayCall<T> seeds the AsyncCoroutine<T> payload with the caller's data so the
		// delayed Action<T> receives it.
		// RCR: CoroutineService.cs StartDelayCall<T> — construct the wrapper with `default` instead of `data` → RED
		// (received stays 0, not 99). 2026-08-02
		public IEnumerator StartDelayCall_WithData_Successfully()
		{
			int received = 0;
			_coroutineService.StartDelayCall<int>(data => received = data, data: 99, delay: 0.05f);

			Assert.AreEqual(0, received);

			yield return new WaitForSeconds(0.2f);

			Assert.AreEqual(99, received);
		}

		// Stopping via IAsyncCoroutine.StopCoroutine MUST flip IsCompleted/IsRunning so
		// editor introspection (Services Explorer Coroutine tab) can drop stopped entries.
		[UnityTest]
		// ADMIT: AsyncCoroutine.StopCoroutine flips IsCompleted so editor introspection can drop stopped entries.
		// RCR: CoroutineService.cs AsyncCoroutine.StopCoroutine — assign `IsCompleted = false` after stopping → RED
		// (IsCompleted stays false). Also reddens AsyncCoroutineStop_CalledTwice_NoOps. 2026-08-02
		public IEnumerator AsyncCoroutineStop_FlipsCompletedAndRunning()
		{
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));

			Assert.IsTrue(asyncCoroutine.IsRunning);
			Assert.IsFalse(asyncCoroutine.IsCompleted);

			asyncCoroutine.StopCoroutine();

			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.IsTrue(asyncCoroutine.IsCompleted);

			yield return null;
		}

		// triggerOnComplete=true MUST invoke the user OnComplete callback.
		[UnityTest]
		// ADMIT: AsyncCoroutine.StopCoroutine(true) invokes the registered OnComplete callback via OnCompleteTrigger.
		// RCR: CoroutineService.cs AsyncCoroutine.StopCoroutine — drop the OnCompleteTrigger() call from the
		// triggerOnComplete branch → RED (testCompleted stays 0). Also reddens AsyncCoroutineStop_CalledTwice_NoOps.
		// 2026-08-02
		public IEnumerator AsyncCoroutineStop_TriggerOnCompleteTrue_InvokesUserCallback()
		{
			int testCompleted = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => testCompleted = 42);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.AreEqual(42, testCompleted);

			yield return null;
		}

		// triggerOnComplete=false MUST suppress the user OnComplete callback.
		[UnityTest]
		// ADMIT: AsyncCoroutine.StopCoroutine honours triggerOnComplete:false by suppressing the user OnComplete callback.
		// RCR: CoroutineService.cs AsyncCoroutine.StopCoroutine — replace the `if (triggerOnComplete)` guard with `if
		// (true)` → RED (testCompleted becomes 42). 2026-08-02
		public IEnumerator AsyncCoroutineStop_TriggerOnCompleteFalse_SuppressesUserCallback()
		{
			int testCompleted = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => testCompleted = 42);

			asyncCoroutine.StopCoroutine(triggerOnComplete: false);

			Assert.AreEqual(0, testCompleted);

			yield return null;
		}

		// User OnComplete callback registered AFTER editor tracking attaches must still fire.
		// Regression guard for v2.0.0 bug where editor tracking lambda assigned via
		// OnComplete(...) overwrote (or was overwritten by) user callbacks.
		[UnityTest]
		// ADMIT: AsyncCoroutine.OnCompleteTrigger invokes the user Action registered through OnComplete(Action), which
		// editor tracking must never overwrite.
		// RCR: CoroutineService.cs AsyncCoroutine.OnCompleteTrigger — drop the `_onComplete?.Invoke()` call → RED
		// (userCallbackFired stays false). Broad: also reddens every other Action-callback test. 2026-08-02
		public IEnumerator AsyncCoroutineOnComplete_RegisteredAfterCreation_FiresOnNaturalCompletion()
		{
			bool userCallbackFired = false;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => userCallbackFired = true);

			yield return asyncCoroutine.Coroutine;

			Assert.IsTrue(userCallbackFired);
			Assert.IsTrue(asyncCoroutine.IsCompleted);
		}

		// StopCoroutine on an already-completed handle must be a no-op — the IsCompleted
		// guard prevents the user OnComplete callback from re-firing and prevents IsRunning
		// state from being clobbered. Pairs with AsyncCoroutineStop_CalledTwice_NoOps below.
		[UnityTest]
		// ADMIT: AsyncCoroutine.Completed sets IsCompleted, which is what makes a later StopCoroutine a no-op after
		// natural completion.
		// RCR: CoroutineService.cs AsyncCoroutine.Completed — assign `IsCompleted = false` → RED (IsCompleted assertion
		// fails and the later stop re-fires the callback). Also reddens the natural-completion siblings. 2026-08-02
		public IEnumerator AsyncCoroutineStop_AfterNaturalCompletion_NoOps()
		{
			int callbackInvocations = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => callbackInvocations++);

			yield return asyncCoroutine.Coroutine;

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(1, callbackInvocations);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.AreEqual(1, callbackInvocations);
		}

		// Two consecutive StopCoroutine calls on the same handle must collapse — the second
		// is a no-op so the user OnComplete callback fires exactly once total. Without the
		// IsCompleted guard, double-stop would fire OnComplete twice and confuse listeners.
		[UnityTest]
		// ADMIT: AsyncCoroutine.StopCoroutine early-returns when already completed, so a double stop fires the user
		// callback exactly once.
		// RCR: CoroutineService.cs AsyncCoroutine.StopCoroutine — delete the `if (IsCompleted) return;` guard → RED
		// (callbackInvocations is 2). Also reddens AsyncCoroutineStop_AfterNaturalCompletion_NoOps. 2026-08-02
		public IEnumerator AsyncCoroutineStop_CalledTwice_NoOps()
		{
			int callbackInvocations = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => callbackInvocations++);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(1, callbackInvocations);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.AreEqual(1, callbackInvocations);

			yield return null;
		}

		[UnityTest]
		// ADMIT: AsyncCoroutine<T>.OnCompleteTrigger reads the live Data property at completion time, not a construction-
		// time snapshot.
		// RCR: CoroutineService.cs AsyncCoroutine<T>.OnCompleteTrigger — invoke with `default` instead of `Data` → RED
		// (observed stays 0). Also reddens the two other Action<T> payload tests. 2026-08-02
		public IEnumerator AsyncCoroutineDataSetter_AfterStart_UpdatesPayload()
		{
			const int initialValue = 5;
			const int mutatedValue = 99;
			int observed = 0;

			var asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(0), initialValue);
			asyncCoroutine.OnComplete(payload => observed = payload);

			asyncCoroutine.Data = mutatedValue;

			yield return asyncCoroutine.Coroutine;

			Assert.AreEqual(mutatedValue, observed);
			Assert.AreEqual(mutatedValue, asyncCoroutine.Data);
		}
	}
}