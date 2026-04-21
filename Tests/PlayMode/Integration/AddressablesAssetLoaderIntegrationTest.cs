using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using GameLovers.Services.AssetsImporter;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	/// <summary>
	/// Integration tests for <see cref="AddressablesAssetLoader"/>.
	/// Marked <see cref="ExplicitAttribute"/> because they require a live Addressables setup
	/// (a valid addressable key must exist in the project's Addressable groups).
	/// Run manually after configuring Addressables in the host project.
	/// </summary>
	[TestFixture]
	[Explicit("Requires a live Addressables setup with a known asset key")]
	public class AddressablesAssetLoaderIntegrationTest
	{
		private AddressablesAssetLoader _loader;

		// Replace with a real Addressable address present in the host project.
		private const string ValidKey = "Assets/Configs/config-placeholder";

		[SetUp]
		public void Init()
		{
			_loader = new AddressablesAssetLoader();
		}

		[UnityTest]
		public IEnumerator LoadAssetAsync_ValidKey_ReturnsAsset()
		{
			TextAsset loaded = null;

			yield return _loader.LoadAssetAsync<TextAsset>(ValidKey, asset => loaded = asset).ToCoroutine();

			Assert.IsNotNull(loaded);
		}

		[UnityTest]
		public IEnumerator UnloadAssetAsync_ReleasesHandle()
		{
			TextAsset loaded = null;

			yield return _loader.LoadAssetAsync<TextAsset>(ValidKey, asset => loaded = asset).ToCoroutine();

			Assert.IsNotNull(loaded);

			yield return _loader.UnloadAssetAsync(loaded).ToCoroutine();
		}
	}
}
