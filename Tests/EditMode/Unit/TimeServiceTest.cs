using System;
using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class TimeServiceTest
	{
		private const float ErrorValue = 0.01f;
		private TimeService _timeService;

		[SetUp]
		public void Init()
		{
			_timeService = new TimeService();
		}

		[Test]
		// ADMIT: TimeService.DateTimeUtcFromUnixTime treats its argument as milliseconds since the Unix epoch, matching
		// UnixTimeNow.
		// RCR: TimeService.cs DateTimeUtcFromUnixTime — add the value as seconds instead of milliseconds → RED (the round
		// trip is off by ~1000x). Also reddens UnityTime_Convertions. 2026-08-02
		public void DateTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, (_timeService.DateTimeUtcFromUnityTime(_timeService.UnityTimeNow) - _timeService.DateTimeUtcNow).TotalMilliseconds);
			Assert.GreaterOrEqual(ErrorValue, (_timeService.DateTimeUtcFromUnixTime(_timeService.UnixTimeNow) - _timeService.DateTimeUtcNow).TotalMilliseconds);
		}

		[Test]
		// ADMIT: TimeService.UnityTimeFromDateTimeUtc/FromUnixTime rebase onto _initialUnityTime so a converted
		// instant lands near UnityTimeNow.
		// RCR: TimeService.cs UnityTimeFromDateTimeUtc — add +1000f to the returned offset → RED. NOTE the assertion is
		// ONE-SIDED (`GreaterOrEqual(ErrorValue, diff)`), so it only catches conversions that grow: negating
		// `_initialUnityTime` instead was observed to stay GREEN. A regression that shrinks the conversion is invisible.
		public void UnityTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnityTimeFromDateTimeUtc(_timeService.DateTimeUtcNow) - _timeService.UnityTimeNow);
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnityTimeFromUnixTime(_timeService.UnixTimeNow) - _timeService.UnityTimeNow);
		}

		[Test]
		// ADMIT: TimeService.UnixTimeFromDateTimeUtc returns milliseconds since the Unix epoch, the unit UnixTimeNow
		// reports in.
		// RCR: TimeService.cs UnixTimeFromDateTimeUtc — add +100000L to the result → RED. NOTE as with the sibling
		// above, the assertion is one-sided: switching TotalMilliseconds to TotalSeconds (a 1000x SHRINK) was observed
		// to stay GREEN. Both fixtures want a two-sided bound on the absolute difference.
		public void UnixTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnixTimeFromDateTimeUtc(_timeService.DateTimeUtcNow) - _timeService.UnixTimeNow);
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnixTimeFromUnityTime(_timeService.UnityTimeNow) - _timeService.UnixTimeNow);
		}

		[Test]
		// ADMIT: TimeService.DateTimeUtcNow folds the accumulated _extraTime into the reported wall clock.
		// RCR: TimeService.cs DateTimeUtcNow — drop the `_extraTime` term → RED (DateTimeUtcNow no longer reaches dateTime
		// + 50.5s). 2026-08-02
		public void AddTime_AllTimeTypes_Successfully()
		{
			var extraTime = 50.5f;
			var extraTimeInMilliseconds = TimeSpan.FromSeconds(extraTime).TotalMilliseconds;
			var dateTime = _timeService.DateTimeUtcNow;
			var unityTime = _timeService.UnityTimeNow;
			var unixTime = _timeService.UnixTimeNow;

			_timeService.AddTime(extraTime);

			Assert.LessOrEqual(0, _timeService.DateTimeUtcNow.CompareTo(dateTime.AddSeconds(extraTime)));
			Assert.GreaterOrEqual(_timeService.UnityTimeNow, unityTime + extraTime);
			Assert.GreaterOrEqual(_timeService.UnixTimeNow, unixTime - extraTimeInMilliseconds);
		}

		[Test]
		// ADMIT: TimeService.AddTime accumulates exactly the requested offset, so UnityTimeNow lands within tolerance of
		// initial + delta.
		// RCR: TimeService.cs AddTime — double the accumulated offset → RED (the Within(0.01) tolerance assertion fails
		// while the coarser Less assertion still passes). 2026-08-02
		public void AddTime_NegativeValue_SubtractsTime()
		{
			var initialUnityTime = _timeService.UnityTimeNow;
			var negativeTime = -10f;

			_timeService.AddTime(negativeTime);

			Assert.Less(_timeService.UnityTimeNow, initialUnityTime);
			Assert.That(_timeService.UnityTimeNow, Is.EqualTo(initialUnityTime + negativeTime).Within(ErrorValue));
		}

		[Test]
		// ADMIT: TimeService.SetInitialTime rebases the clock onto the supplied DateTime.
		// RCR: TimeService.cs SetInitialTime — drop the `_initialTime` assignment → RED (DateTimeUtcNow stays on the
		// constructor's DateTime.Now, years away from 2025-01-01). 2026-08-02
		public void SetInitialTime_ResetsTimeBase()
		{
			// SetInitialTime acts as a "reset" by synchronizing the time base
			var customInitialTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			
			_timeService.SetInitialTime(customInitialTime);
			
			// After setting initial time, DateTimeUtcNow should be close to the custom time
			// (plus any time that has passed since realtimeSinceStartup was captured)
			var now = _timeService.DateTimeUtcNow;
			
			// The difference should be very small (just the time since SetInitialTime was called)
			Assert.That((now - customInitialTime).TotalSeconds, Is.LessThan(1.0));
		}
	}
}
