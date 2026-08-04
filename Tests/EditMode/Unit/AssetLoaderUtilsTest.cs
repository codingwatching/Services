using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using GameLovers.Services.AssetsImporter;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AssetLoaderUtilsTest
	{
		[Test]
		public void Interleaved_EmptyInput_ReturnsEmpty()
		{
			var result = AssetLoaderUtils.Interleaved(new List<Task<int>>());

			Assert.AreEqual(0, result.Length);
		}

		[Test]
		// ADMIT: AssetLoaderUtils.Interleaved fills the next free bucket per completion via the interlocked counter, so
		// bucket N resolves to the Nth task to finish.
		// RCR: AssetLoaderUtils.cs Interleaved.Continuation — always target bucket 0 → RED (buckets[1] and buckets[2]
		// never complete). 2026-08-02
		public void Interleaved_CompletesInCompletionOrder()
		{
			var tcs1 = new TaskCompletionSource<int>();
			var tcs2 = new TaskCompletionSource<int>();
			var tcs3 = new TaskCompletionSource<int>();

			var tasks = new List<Task<int>> { tcs1.Task, tcs2.Task, tcs3.Task };
			var buckets = AssetLoaderUtils.Interleaved(tasks);

			Assert.AreEqual(3, buckets.Length);

			// Complete tasks out-of-order: 3, 1, 2
			tcs3.SetResult(30);
			tcs1.SetResult(10);
			tcs2.SetResult(20);

			// Each bucket resolves to the task that completed in that slot
			Assert.IsTrue(buckets[0].IsCompleted);
			Assert.AreEqual(30, buckets[0].Result.Result);
			Assert.IsTrue(buckets[1].IsCompleted);
			Assert.AreEqual(10, buckets[1].Result.Result);
			Assert.IsTrue(buckets[2].IsCompleted);
			Assert.AreEqual(20, buckets[2].Result.Result);
		}
	}
}
