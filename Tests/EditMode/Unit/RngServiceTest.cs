using System;
using GameLovers.GameData;
using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class RngServiceTest
	{
		private RngService _rngService;
		private RngData _rngData;
		private const int Seed = 12345;

		[SetUp]
		public void Init()
		{
			_rngData = RngService.CreateRngData(Seed);
			_rngService = new RngService(_rngData);
		}


		[Test]
		// ADMIT: RngService.PeekRange(int,int,bool) draws from a copy of the state so Peek never advances the live
		// sequence.
		// RCR: RngService.cs PeekRange(int,int,bool) — pass the live `_rngData.State` instead of a copy → RED (the two
		// Peek reads differ). Also reddens PeekRange_IntBounds. 2026-08-02
		public void Peek_DoesNotAdvanceState()
		{
			var peeked = _rngService.Peek;
			var peeked2 = _rngService.Peek;
			var next = _rngService.Next;

			Assert.AreEqual(peeked, peeked2);
			Assert.AreEqual(peeked, next);
			Assert.AreEqual(1, _rngService.Counter);
		}

		[Test]
		// ADMIT: RngService.Range's degenerate-range early return yields exactly min when the bounds are within
		// floatP.Epsilon.
		// RCR: RngService.cs Range(floatP,floatP,int[],bool) — return `min + 1` from the equal-bounds early return → RED
		// (11 instead of 10). Also reddens the annotated float inclusive sibling. 2026-08-02
		public void Range_MinEqualsMax_ReturnsMin()
		{
			const int minMax = 10;
			Assert.AreEqual(minMax, _rngService.Range(minMax, minMax, true));
		}

		[Test]
		// ADMIT: RngService.Range(int,int,int[],bool) widens both int bounds to floatP before the inverted-range guard
		// runs, so Range(10, 5) reaches the throw.
		// RCR: RngService.cs Range(int,int,int[],bool) — widen min as 0 instead → RED (bounds become 0..5, the guard never
		// fires and no IndexOutOfRangeException is thrown). 2026-08-02
		public void Range_MinGreaterThanMax_ThrowsException()
		{
			Assert.Throws<IndexOutOfRangeException>(() => _rngService.Range(10, 5));
		}

		[Test]
		// ADMIT: RngService.Range(floatP,floatP,bool) must accept a degenerate closed range where min == max and
		// maxInclusive is true, rather than rejecting it as inverted.
		// RCR: RngService.cs Range — change `if (min > max || ...)` to `if (min >= max || ...)` → RED
		// (Range(2.5f, 2.5f, true) throws IndexOutOfRangeException instead of returning 2.5f). 2026-07-31
		public void Range_FloatMinEqualsMaxWithMaxInclusive_ReturnsMin()
		{
			var minMax = (floatP)2.5f;

			floatP result = default;
			Assert.DoesNotThrow(() => result = _rngService.Range(minMax, minMax, true));
			Assert.AreEqual(minMax, result);
		}

		[Test]
		// ADMIT: RngService.Range(floatP,floatP,bool) must throw for an exclusive (maxInclusive:false) empty range
		// rather than falling through to the equal-bounds early return and returning min.
		// RCR: RngService.cs Range — drop the whole `|| (!maxInclusive && ...)` disjunct, leaving `if (min > max)`
		// → RED (returns 2.5f instead of throwing). Dropping only the `!maxInclusive &&` prefix is NOT valid here:
		// the remaining epsilon term is true for min == max regardless, and it falsifies the inclusive sibling
		// test instead. 2026-08-02
		public void Range_FloatMinEqualsMaxWithMaxExclusive_ThrowsIndexOutOfRange()
		{
			var minMax = (floatP)2.5f;

			Assert.Throws<IndexOutOfRangeException>(() => _rngService.Range(minMax, minMax, false));
		}

		[Test]
		// ADMIT: RngService.Restore(int) rebuilds the state array from the seed so the sequence replays from the restored
		// count.
		// RCR: RngService.cs Restore(int) — drop the state rebuild → RED (the next value after Restore is not the
		// previously peeked value). 2026-08-02
		public void Restore_ToPastCount_ReproducesSequence()
		{
			_ = _rngService.Next;
			_ = _rngService.Next;
			var count = _rngService.Counter;
			var nextValue = _rngService.Peek;
			
			_ = _rngService.Next;
			_ = _rngService.Next;
			
			_rngService.Restore(count);
			
			Assert.AreEqual(count, _rngService.Counter);
			Assert.AreEqual(nextValue, _rngService.Next);
		}

		[Test]
		// ADMIT: RngService.Restore(int) writes the requested count back onto RngData, including counts ahead of the
		// current one.
		// RCR: RngService.cs Restore(int) — zero the count assignment → RED (Counter is 0, not 5). Also reddens
		// Restore_ToPastCount's Counter assertion. 2026-08-02
		public void Restore_ToFutureCount_AdvancesCorrectly()
		{
			var count = 5;
			_rngService.Restore(count);
			
			Assert.AreEqual(count, _rngService.Counter);
		}

		[Test]
		// ADMIT: RngService.CopyRngState allocates a fresh array, so mutating the live state cannot reach a previously
		// taken copy.
		// RCR: RngService.cs CopyRngState — alias the caller's array instead of allocating → RED (the copy tracks the
		// advanced state, AreNotEqual fails). Broad: also reddens every Peek test. 2026-08-02
		public void CopyRngState_CreatesIndependentCopy()
		{
			var stateCopy = RngService.CopyRngState(_rngData.State);
			_ = _rngService.Next;
			
			// Manually advance the copy
			// Note: NextNumber is private, so we'll just check that the copy remains the same
			Assert.AreNotEqual(_rngData.State, stateCopy); 
		}

		[Test]
		// ADMIT: RngService.CreateRngData records the caller's seed on the RngData it returns.
		// RCR: RngService.cs CreateRngData — store 0 instead of `seed` → RED (data.Seed is 0, not 12345). Also reddens
		// Restore_ToPastCount, which rebuilds from the stored seed. 2026-08-02
		public void CreateRngData_InitializesCorrectly()
		{
			var data = RngService.CreateRngData(Seed);
			Assert.AreEqual(Seed, data.Seed);
			Assert.AreEqual(0, data.Count);
			Assert.IsNotNull(data.State);
			Assert.AreEqual(56, data.State.Length);
		}


		[Test]
		// ADMIT: RngService.Peekfloat draws through PeekRange, which works on a copy of the state, so repeated reads
		// return the same value and never advance the live sequence.
		// RCR: RngService.cs Peekfloat — narrow the range to `(floatP) 1000f` → RED. NOTE making PeekRange consume the
		// LIVE state was observed to stay GREEN here: at `floatP.MaxValue` consecutive draws saturate to the same
		// floatP, so this test is precision-blind to the very advance its name claims to catch.
		public void Peekfloat_DoesNotAdvanceState()
		{
			floatP peeked1 = _rngService.Peekfloat;
			floatP peeked2 = _rngService.Peekfloat;
			floatP next = _rngService.Nextfloat;

			Assert.AreEqual(peeked1, peeked2);
			Assert.AreEqual(peeked1, next);
			Assert.AreEqual(1, _rngService.Counter);
		}

		[Test]
		// ADMIT: exercises RngService.Range(floatP,floatP,int[],bool)'s scale-and-offset path; no unique one-line pin.
		// RCR: no isolated mutation -- reddens under Range_IntStaticOverload_StaysWithinBoundsAndAdvancesState's
		// mutation (offset by max instead of min; radius 5, observed). Shared-path coverage, not a duplicate.
		public void RangeFloat_ReturnsValueInRange()
		{
			floatP min = (floatP)0f;
			floatP max = (floatP)1f;

			for (var i = 0; i < 20; i++)
			{
				floatP value = _rngService.Range(min, max);
				Assert.GreaterOrEqual((float)value, (float)min);
				Assert.LessOrEqual((float)value, (float)max);
			}
		}

		[Test]
		// ADMIT: RngService.Range(floatP,floatP,bool) is the consuming overload and increments RngData.Count; PeekRange
		// must not.
		// RCR: RngService.cs Range(floatP,floatP,bool) — drop the `_rngData.Count++` → RED (Counter is 0 after the
		// consuming Range call). Also reddens Peekfloat_DoesNotAdvanceState's Counter assertion. 2026-08-02
		public void PeekRangeFloat_DoesNotAdvanceState()
		{
			floatP min = (floatP)0f;
			floatP max = (floatP)1f;

			floatP peeked1 = _rngService.PeekRange(min, max);
			floatP peeked2 = _rngService.PeekRange(min, max);

			Assert.AreEqual(peeked1, peeked2);
			Assert.AreEqual(0, _rngService.Counter);

			floatP actual = _rngService.Range(min, max);
			Assert.AreEqual(peeked1, actual);
			Assert.AreEqual(1, _rngService.Counter);
		}

		[Test]
		// ADMIT: RngService.Range(int,int,bool) is the consuming overload and increments RngData.Count; PeekRange must
		// not.
		// RCR: RngService.cs Range(int,int,bool) — drop the `_rngData.Count++` → RED (Counter is 0 after the consuming
		// Range call). Also reddens Peek_DoesNotAdvanceState's Counter assertion. 2026-08-02
		public void PeekRange_IntBounds_ReturnsValueInRange_DoesNotAdvance()
		{
			const int min = 5;
			const int max = 50;

			var peeked1 = _rngService.PeekRange(min, max, false);
			var peeked2 = _rngService.PeekRange(min, max, false);

			Assert.AreEqual(peeked1, peeked2);
			Assert.AreEqual(0, _rngService.Counter);
			Assert.GreaterOrEqual(peeked1, min);
			Assert.Less(peeked1, max);

			var actual = _rngService.Range(min, max, false);
			Assert.AreEqual(peeked1, actual);
			Assert.AreEqual(1, _rngService.Counter);
		}

		[Test]
		// ADMIT: RngService.Range draws from NextNumber, so repeated calls over a wide closed range produce more than one
		// distinct value.
		// RCR: RngService.cs Range(floatP,floatP,int[],bool) — hard-code the draw to 0 → RED (every sample is min,
		// seenValues.Count is 1). Also reddens Range_IntStaticOverload's variance assertion. 2026-08-02
		public void Range_IntMaxInclusiveTrue_StaysWithinClosedRangeAndVaries()
		{
			const int min = 0;
			const int max = 100;
			var seenValues = new System.Collections.Generic.HashSet<int>();

			for (var i = 0; i < 200; i++)
			{
				var value = _rngService.Range(min, max, true);

				Assert.GreaterOrEqual(value, min);
				Assert.LessOrEqual(value, max);
				seenValues.Add(value);
			}

			Assert.Greater(seenValues.Count, 1);
		}

		[Test]
		// ADMIT: RngService.Range(floatP,floatP,int[],bool) throws for inverted bounds under both maxInclusive settings.
		// RCR: RngService.cs Range(floatP,floatP,int[],bool) — return min instead of throwing → RED (neither Assert.Throws
		// fires). Also reddens the annotated exclusive-empty-range sibling. 2026-08-02
		public void Range_FloatPStaticOverload_InBoundsHappyPath_AndThrowsOnInvertedBounds()
		{
			var state = RngService.CopyRngState(_rngData.State);
			floatP min = (floatP)0f;
			floatP max = (floatP)10f;

			var value = RngService.Range(min, max, state, false);

			Assert.GreaterOrEqual((float)value, (float)min);
			Assert.LessOrEqual((float)value, (float)max);

			Assert.Throws<IndexOutOfRangeException>(() => RngService.Range((floatP)10f, (floatP)0f, state, true));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.Range((floatP)10f, (floatP)0f, state, false));
		}

		[Test]
		// ADMIT: RngService.CopyRngState rejects any state array whose length is not the 56-entry Knuth state.
		// RCR: RngService.cs CopyRngState — drop the length term from the guard → RED (new int[0]/int[10]/int[55] no
		// longer throw). 2026-08-02
		public void CopyRngState_WrongLengthInput_Throws()
		{
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(null));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[0]));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[10]));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[55]));
		}

		[Test]
		// ADMIT: RngService.Restore(int,int) advances the freshly generated state exactly `count` times so it matches
		// count iterative Next calls.
		// RCR: RngService.cs Restore(int,int) — advance count - 1 times → RED (staticState differs from the iteratively
		// advanced state). Also reddens Restore_ToPastCount. 2026-08-02
		public void Restore_StaticOverload_AgreesWithIterativeNextOnFreshSeed()
		{
			const int count = 7;

			var staticState = RngService.Restore(count, Seed);

			var freshData = RngService.CreateRngData(Seed);
			var freshService = new RngService(freshData);
			for (var i = 0; i < count; i++)
			{
				_ = freshService.Next;
			}

			Assert.AreEqual(56, staticState.Length);
			Assert.AreEqual(freshData.State, staticState);
		}

		[Test]
		// ADMIT: RngService.Range offsets the scaled draw by min, keeping results inside [min, max).
		// RCR: RngService.cs Range(floatP,floatP,int[],bool) — offset by max instead → RED (values land in [max,
		// max+range), Assert.Less fails). Broad: also reddens every other bounded-range test. 2026-08-02
		public void Range_IntStaticOverload_StaysWithinBoundsAndAdvancesState()
		{
			const int min = -50;
			const int max = 50;
			var state = RngService.CopyRngState(_rngData.State);
			var stateBefore = RngService.CopyRngState(state);
			var seenValues = new System.Collections.Generic.HashSet<int>();

			for (var i = 0; i < 50; i++)
			{
				var value = RngService.Range(min, max, state, false);
				Assert.GreaterOrEqual(value, min);
				Assert.Less(value, max);
				seenValues.Add(value);
			}

			Assert.Greater(seenValues.Count, 1);
			Assert.AreNotEqual(stateBefore, state);
		}
	}
}
