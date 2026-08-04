using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using GameLovers.GameData;
using GameLovers.Services;
using GameLovers.Services.AssetsImporter;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AssetResolverServiceTest
	{
		private class TestSpriteConfigs : AssetConfigsScriptableObject<int, Sprite> { }

		private AssetResolverService _service;

		[SetUp]
		public void Init()
		{
			_service = new AssetResolverService();
		}

		[Test]
		// ADMIT: AssetResolverService.AddAssets<TId> must store the caller's AssetReference under its id in the
		// per-asset-type map that every RequestAsset / UnloadAssets lookup reads.
		// RCR: AssetResolverService.cs AddAssets<TId> — store `null` instead of `assets[i].Value` → RED (AreSame
		// fails). Broad: also reddens the other AssetMap assertions in this fixture. 2026-08-04
		public void AddAsset_NewType_RegistersEntry()
		{
			var assetRef = new AssetReference();

			_service.AddAsset<int>(typeof(Sprite), 1, assetRef);

			var map = (Dictionary<int, AssetReference>) _service.AssetMap[typeof(Sprite)][typeof(int)];

			Assert.AreEqual(1, map.Count);
			Assert.AreSame(assetRef, map[1]);
		}

		[Test]
		// ADMIT: AssetResolverService.AddAssets<TId> merges a second registration for the same (assetType, idType)
		// pair into the existing map instead of replacing it.
		// RCR: AssetResolverService.cs AddAssets<TId> — delete `assetReferences.Add(asset.Key, asset.Value);` from the
		// merge loop → RED (Count is 1 and ref1 is gone). Unique: no other test reaches the merge branch. 2026-08-04
		public void AddAssets_DuplicateType_MergesEntries()
		{
			var ref1 = new AssetReference();
			var ref2 = new AssetReference();

			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, ref1)
			});
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(2, ref2)
			});

			var map = (Dictionary<int, AssetReference>) _service.AssetMap[typeof(Sprite)][typeof(int)];

			Assert.AreEqual(2, map.Count);
			Assert.AreSame(ref1, map[1]);
			Assert.AreSame(ref2, map[2]);
		}

		[Test]
		// ADMIT: AssetResolverService.UnloadAssets<TId,TAsset>(bool) warns and returns instead of throwing when the asset
		// type was never registered.
		// RCR: AssetResolverService.cs UnloadAssets(bool) — suppress the Debug.LogWarning → RED (LogAssert.Expect(Warning)
		// is unmet). Also reddens UnloadAssets_ClearReferences_RemovesMap. 2026-08-02
		public void UnloadAssets_UnknownType_DoesNotThrow()
		{
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(false));
		}

		[Test]
		// ADMIT: AssetResolverService.UnloadAssets<TId,TAsset>(bool) drops the id-map entry when clearReferences is set.
		// RCR: AssetResolverService.cs UnloadAssets(bool) — skip the map removal → RED (the follow-up call still resolves
		// the map, so the expected warning never fires). 2026-08-02
		public void UnloadAssets_ClearReferences_RemovesMap()
		{
			var assetRef = new AssetReference();
			_service.AddAsset<int>(typeof(Sprite), 1, assetRef);
			_service.UnloadAssets<int, Sprite>(clearReferences: true);

			// After clear, a second clear should warn (map entry removed)
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
			_service.UnloadAssets<int, Sprite>(clearReferences: false);
		}

		[Test]
		// ADMIT: AssetResolverService.SelectAsset must return the configured error placeholder, not the real
		// not-yet-loaded reference, when `!isDone` — the package's principal silent-failure mode.
		// RCR: AssetResolverService.cs SelectAsset — invert the Sprite branch to
		// `isDone ? errorSprite as TAsset : asset as TAsset` → RED (returns the real Sprite). 2026-08-01
		public void SelectAsset_WhenReferenceNotDone_ReturnsErrorPlaceholderNotNull()
		{
			var texture = Texture2D.blackTexture;
			var realSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
			var errorSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);

			var result = AssetResolverService.SelectAsset<Sprite>(typeof(Sprite), realSprite, isDone: false,
				instantiate: false, errorSprite: errorSprite, errorCube: null, errorMaterial: null, errorClip: null);

			Assert.IsNotNull(result);
			Assert.AreSame(errorSprite, result);
			Assert.AreNotSame(realSprite, result);

			UnityEngine.Object.DestroyImmediate(realSprite);
			UnityEngine.Object.DestroyImmediate(errorSprite);
		}

		[Test]
		// ADMIT: AssetResolverService.AddDebugConfigs stores the error Material used by SelectAsset's not-loaded fallback.
		// RCR: AssetResolverService.cs AddDebugConfigs — null the `_errorMaterial` assignment → RED (the reflected field
		// is null, AreSame fails). 2026-08-02
		public void AddDebugConfigs_StoresAllProvided()
		{
			var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
			var mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));

			Assert.DoesNotThrow(() => _service.AddDebugConfigs(errorMaterial: mat));

			var field = typeof(AssetResolverService).GetField("_errorMaterial",
				BindingFlags.NonPublic | BindingFlags.Instance);

			Assert.IsNotNull(field);
			Assert.AreSame(mat, field.GetValue(_service));
		}

		[Test]
		// ADMIT: AssetResolverService.UnloadAssets<TId,TAsset>(bool, AssetConfigsScriptableObject) clears only the ids
		// the container lists, leaving every other registered id in the map.
		// RCR: AssetResolverService.cs UnloadAssets(bool, AssetConfigsScriptableObject) — delete
		// `dictionary.Remove(pair.Key);` → RED (Count is 3, not 1). Unique to that overload. 2026-08-04
		public void UnloadAssets_WithAssetConfigsContainer_ReleasesAssetsInContainer()
		{
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, new AssetReference()),
				new Pair<int, AssetReference>(2, new AssetReference()),
				new Pair<int, AssetReference>(3, new AssetReference())
			});

			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, new AssetReference()),
				new Pair<int, AssetReference>(2, new AssetReference())
			};

			_service.UnloadAssets<int, Sprite>(clearReferences: true, assetConfigs: so);

			var map = (Dictionary<int, AssetReference>) _service.AssetMap[typeof(Sprite)][typeof(int)];

			Assert.AreEqual(1, map.Count);
			Assert.IsTrue(map.ContainsKey(3));

			UnityEngine.Object.DestroyImmediate(so);
		}

		[Test]
		// ADMIT: AssetResolverService.UnloadAssets<TId,TAsset>(bool, params TId[]) removes only the listed ids from the
		// id map rather than clearing it wholesale.
		// RCR: AssetResolverService.cs UnloadAssets(bool, params TId[]) — change `dictionary.Remove(id);` to
		// `dictionary.Clear();` → RED (Count is 0, not 1). Unique to that overload. 2026-08-04
		public void UnloadAssets_WithIdsArray_ReleasesOnlyMatching()
		{
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(10, new AssetReference()),
				new Pair<int, AssetReference>(20, new AssetReference()),
				new Pair<int, AssetReference>(30, new AssetReference())
			});

			_service.UnloadAssets<int, Sprite>(clearReferences: true, 10, 20);

			var map = (Dictionary<int, AssetReference>) _service.AssetMap[typeof(Sprite)][typeof(int)];

			Assert.AreEqual(1, map.Count);
			Assert.IsTrue(map.ContainsKey(30));
		}

		[Test]
		// ADMIT: IAssetAdderService.AddConfigs<TId,TAsset>, a C# 8 default interface method, forwards the container's
		// own Configs list to AssetResolverService.AddAssets.
		// RCR: AssetResolverService.cs IAssetAdderService.AddConfigs — forward an empty list instead of
		// `configs.Configs` → RED (Count is 0, not 1). Unique: only this test goes through the default method. 2026-08-04
		public void AddConfigs_DelegatesToAddAssets()
		{
			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(7, new AssetReference())
			};

			IAssetAdderService adderService = _service;
			adderService.AddConfigs<int, Sprite>(so);

			var map = (Dictionary<int, AssetReference>) _service.AssetMap[typeof(Sprite)][typeof(int)];

			Assert.AreEqual(1, map.Count);
			Assert.IsTrue(map.ContainsKey(7));

			UnityEngine.Object.DestroyImmediate(so);
		}

		[Test]
		public async System.Threading.Tasks.Task RequestAsset_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.RequestAsset<int, Sprite>(99);
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		// ADMIT: AssetResolverService.LoadSceneAsync<TId> throws MissingMemberException when no scene AssetReference was
		// registered for the id.
		// RCR: AssetResolverService.cs LoadSceneAsync<TId> — return default instead of throwing → RED (no
		// MissingMemberException is caught). 2026-08-02
		public async System.Threading.Tasks.Task LoadSceneAsync_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.LoadSceneAsync(7);
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		// ADMIT: AssetResolverService.RequestAsset<TId,TAsset,TData> throws MissingMemberException when no AssetReference
		// was registered for the id.
		// RCR: AssetResolverService.cs RequestAsset<TId,TAsset,TData> — return null instead of throwing → RED (no
		// MissingMemberException is caught). Also reddens the two-parameter overload's test, which delegates here.
		// 2026-08-02
		public async System.Threading.Tasks.Task RequestAsset_ThreeParamWithData_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.RequestAsset<int, Sprite, string>(99, "payload");
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		// ADMIT: AssetResolverService.LoadAllAssets<TId,TAsset> throws MissingMemberException when the asset type was
		// never registered.
		// RCR: AssetResolverService.cs LoadAllAssets<TId,TAsset> — return the empty list instead of throwing → RED (no
		// MissingMemberException is caught). 2026-08-02
		public async System.Threading.Tasks.Task LoadAllAssets_UnknownAssetType_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.LoadAllAssets<int, Sprite>();
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		// ADMIT: AssetResolverService.UnloadSceneAsync<TId> logs a warning and completes rather than throwing for an
		// unregistered scene id.
		// RCR: AssetResolverService.cs UnloadSceneAsync<TId> — suppress the Debug.LogWarning → RED
		// (LogAssert.Expect(Warning) is unmet). 2026-08-02
		public async System.Threading.Tasks.Task UnloadSceneAsync_UnknownId_LogsWarningAndCompletes()
		{
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));

			Exception caught = null;
			try
			{
				await _service.UnloadSceneAsync(123);
			}
			catch (Exception ex)
			{
				caught = ex;
			}

			Assert.IsNull(caught);
		}
	}
}
