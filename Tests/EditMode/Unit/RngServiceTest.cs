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
		public void Next_SameSeed_ReturnsDeterministicSequence()
		{
			var sequence1 = new int[10];
			for (var i = 0; i < 10; i++) sequence1[i] = _rngService.Next;

			var data2 = RngService.CreateRngData(Seed);
			var rng2 = new RngService(data2);
			var sequence2 = new int[10];
			for (var i = 0; i < 10; i++) sequence2[i] = rng2.Next;

			Assert.AreEqual(sequence1, sequence2);
		}

		[Test]
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
		public void Range_MinEqualsMax_ReturnsMin()
		{
			const int minMax = 10;
			Assert.AreEqual(minMax, _rngService.Range(minMax, minMax, true));
		}

		[Test]
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
		public void Restore_ToFutureCount_AdvancesCorrectly()
		{
			var count = 5;
			_rngService.Restore(count);
			
			Assert.AreEqual(count, _rngService.Counter);
		}

		[Test]
		public void CopyRngState_CreatesIndependentCopy()
		{
			var stateCopy = RngService.CopyRngState(_rngData.State);
			_ = _rngService.Next;
			
			// Manually advance the copy
			// Note: NextNumber is private, so we'll just check that the copy remains the same
			Assert.AreNotEqual(_rngData.State, stateCopy); 
		}

		[Test]
		public void CreateRngData_InitializesCorrectly()
		{
			var data = RngService.CreateRngData(Seed);
			Assert.AreEqual(Seed, data.Seed);
			Assert.AreEqual(0, data.Count);
			Assert.IsNotNull(data.State);
			Assert.AreEqual(56, data.State.Length);
		}

		[Test]
		public void Nextfloat_ReturnsDeterministicSequence()
		{
			floatP f1 = _rngService.Nextfloat;
			floatP f2 = _rngService.Nextfloat;

			var data2 = RngService.CreateRngData(Seed);
			var rng2 = new RngService(data2);
			floatP f1b = rng2.Nextfloat;
			floatP f2b = rng2.Nextfloat;

			Assert.AreEqual(f1, f1b);
			Assert.AreEqual(f2, f2b);
		}

		[Test]
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
		public void CopyRngState_WrongLengthInput_Throws()
		{
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(null));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[0]));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[10]));
			Assert.Throws<IndexOutOfRangeException>(() => RngService.CopyRngState(new int[55]));
		}

		[Test]
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
