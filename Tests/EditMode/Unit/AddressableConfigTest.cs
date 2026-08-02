using NUnit.Framework;
using GameLovers.Services.AssetsImporter;
using UnityEngine.SceneManagement;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AddressableConfigTest
	{
		private AddressableConfig _sceneConfig;
		private AddressableConfig _spriteConfig;

		[SetUp]
		public void Init()
		{
			_sceneConfig = new AddressableConfig(0, "Scenes/MainMenu.unity", "Assets/Scenes/MainMenu.unity",
				typeof(Scene), new[] { "scenes" });
			_spriteConfig = new AddressableConfig(1, "Sprites/hero", "Assets/Sprites/hero.png",
				typeof(UnityEngine.Sprite), new string[0]);
		}

		[Test]
		// ADMIT: AddressableConfig.GetSceneName starts the substring one character past the last '/' so the separator is
		// not part of the name.
		// RCR: AddressableConfig.cs GetSceneName — drop the `+ 1` past the slash → RED ("/MainMenu" instead of
		// "MainMenu"). Also reddens GetSceneName_WithAddressWithoutExtension. 2026-08-02
		public void GetSceneName_WithSceneAssetType_ReturnsName()
		{
			Assert.AreEqual("MainMenu", _sceneConfig.GetSceneName());
		}

		[Test]
		// ADMIT: AddressableConfig.GetSceneName rejects a config whose AssetType is not Scene instead of returning a bogus
		// name.
		// RCR: AddressableConfig.cs GetSceneName — guard on `AssetType == null` instead → RED (the Sprite config returns
		// "hero" and no InvalidOperationException is thrown). 2026-08-02
		public void GetSceneName_WithNonSceneAssetType_Throws()
		{
			Assert.Throws<System.InvalidOperationException>(() => _spriteConfig.GetSceneName());
		}

		[Test]
		// ADMIT: AddressableConfigComparer.Equals compares configs by Id, so two configs sharing an Id are equal
		// regardless of address.
		// RCR: AddressableConfig.cs AddressableConfigComparer.Equals — invert the Id comparison → RED (Assert.IsTrue fails
		// for two Id-0 configs). 2026-08-02
		public void AddressableConfigComparer_EqualIds_ReturnsTrue()
		{
			var comparer = new AddressableConfigComparer();
			var other = new AddressableConfig(0, "Other", "Assets/Other", typeof(Scene), new string[0]);

			Assert.IsTrue(comparer.Equals(_sceneConfig, other));
		}

		[Test]
		// ADMIT: AddressableConfigComparer.GetHashCode returns the config Id so it stays consistent with the Id-based
		// Equals.
		// RCR: AddressableConfig.cs AddressableConfigComparer.GetHashCode — return a constant 0 → RED (the Id-1 config
		// hashes to 0, not 1). 2026-08-02
		public void AddressableConfigComparer_GetHashCode_ReturnsId()
		{
			var comparer = new AddressableConfigComparer();

			Assert.AreEqual(0, comparer.GetHashCode(_sceneConfig));
			Assert.AreEqual(1, comparer.GetHashCode(_spriteConfig));
		}

		[Test]
		// ADMIT: AddressableConfig.GetSceneName falls back to index 0 when the address has no '/' separator.
		// RCR: AddressableConfig.cs GetSceneName — start the no-slash fallback at 1 → RED ("ainMenu" instead of
		// "MainMenu"); the slash-bearing siblings stay green. 2026-08-02
		public void GetSceneName_WithAddressWithoutSlash_ReturnsFullAddress()
		{
			var rootSceneConfig = new AddressableConfig(2, "MainMenu.unity", "Assets/MainMenu.unity",
				typeof(Scene), new string[0]);

			Assert.AreEqual("MainMenu", rootSceneConfig.GetSceneName());
		}

		[Test]
		// ADMIT: AddressableConfig.GetSceneName clamps the end index to Address.Length when the address carries no '.'
		// extension.
		// RCR: AddressableConfig.cs GetSceneName — clamp to Address.Length - 1 → RED ("MyScen" instead of "MyScene"); the
		// extension-bearing siblings stay green. 2026-08-02
		public void GetSceneName_WithAddressWithoutExtension_ReturnsFullAddress()
		{
			var noExtensionSceneConfig = new AddressableConfig(3, "Scenes/MyScene", "Assets/Scenes/MyScene",
				typeof(Scene), new string[0]);

			Assert.AreEqual("MyScene", noExtensionSceneConfig.GetSceneName());
		}
	}
}
