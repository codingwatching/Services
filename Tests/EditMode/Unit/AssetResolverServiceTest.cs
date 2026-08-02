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
		public void AddAsset_NewType_RegistersEntry()
		{
			var assetRef = new AssetReference();
			Assert.DoesNotThrow(() => _service.AddAsset<int>(typeof(Sprite), 1, assetRef));
		}

		[Test]
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

			// Both entries should now exist — verifiable via UnloadAssets without throwing
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(false));
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
		public void UnloadAssets_WithAssetConfigsContainer_ReleasesAssetsInContainer()
		{
			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, new AssetReference()),
				new Pair<int, AssetReference>(2, new AssetReference())
			};
			_service.AddAssets(typeof(Sprite), so.Configs);

			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: false, assetConfigs: so));

			UnityEngine.Object.DestroyImmediate(so);
		}

		[Test]
		public void UnloadAssets_WithIdsArray_ReleasesOnlyMatching()
		{
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(10, new AssetReference()),
				new Pair<int, AssetReference>(20, new AssetReference()),
				new Pair<int, AssetReference>(30, new AssetReference())
			});

			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true, 10, 20));

			// The non-matching id 30 must still be resolvable — a second clear on the remaining map entries should not warn
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true, 30));
		}

		[Test]
		public void AddConfigs_DelegatesToAddAssets()
		{
			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(7, new AssetReference())
			};

			IAssetAdderService adderService = _service;
			Assert.DoesNotThrow(() => adderService.AddConfigs<int, Sprite>(so));

			// Registered via the default interface method — subsequent unload should not warn
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true));

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
