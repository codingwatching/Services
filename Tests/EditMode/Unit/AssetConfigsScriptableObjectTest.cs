using System.Collections.Generic;
using GameLovers.GameData;
using GameLovers.Services.AssetsImporter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AssetConfigsScriptableObjectTest
	{
		private class TestAssetConfigs : AssetConfigsScriptableObject<int, Sprite> { }

		[Test]
		// ADMIT: AssetConfigsScriptableObjectBase<TId,TAsset>.OnAfterDeserialize projects the serialized Configs list into
		// ConfigsDictionary.
		// RCR: AssetConfigsScriptableObject.cs OnAfterDeserialize — drop the per-pair `dictionary.Add` → RED
		// (ConfigsDictionary.Count is 0, not 2). 2026-08-02
		public void OnAfterDeserialize_RebuildsDictionaryFromConfigs()
		{
			var so = ScriptableObject.CreateInstance<TestAssetConfigs>();

			var refA = new AssetReference();
			var refB = new AssetReference();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, refA),
				new Pair<int, AssetReference>(2, refB)
			};

			so.OnAfterDeserialize();

			Assert.IsNotNull(so.ConfigsDictionary);
			Assert.AreEqual(2, so.ConfigsDictionary.Count);
			Assert.AreSame(refA, so.ConfigsDictionary[1]);
			Assert.AreSame(refB, so.ConfigsDictionary[2]);

			Object.DestroyImmediate(so);
		}
	}
}
