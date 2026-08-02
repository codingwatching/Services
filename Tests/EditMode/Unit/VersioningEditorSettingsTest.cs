using GameLovers.Services.Versioning.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class VersioningEditorSettingsTest
	{
		[Test]
		// ADMIT: VersioningEditorSettings.IsValidResourcesPath rejects an empty or whitespace path before normalising it.
		// RCR: VersioningEditorSettings.cs IsValidResourcesPath — return true from the empty branch → RED (Assert.IsFalse
		// fails). 2026-08-02
		public void IsValidResourcesPath_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		// ADMIT: VersioningEditorSettings.IsValidResourcesPath requires an Assets/ prefix so the folder is inside the
		// AssetDatabase.
		// RCR: VersioningEditorSettings.cs IsValidResourcesPath — return true from the prefix branch → RED
		// ("Configs/Resources" is accepted). 2026-08-02
		public void IsValidResourcesPath_NoAssetsPrefix_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Configs/Resources", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		// ADMIT: VersioningEditorSettings.IsValidResourcesPath rejects `..` segments that would escape the project tree.
		// RCR: VersioningEditorSettings.cs IsValidResourcesPath — return true from the dot-dot branch → RED
		// ("Assets/../Resources" is accepted). 2026-08-02
		public void IsValidResourcesPath_DotDotSegment_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Assets/../Resources", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		// ADMIT: VersioningEditorSettings.IsValidResourcesPath requires a segment named exactly "Resources" so
		// Resources.Load can find version-data at runtime.
		// RCR: VersioningEditorSettings.cs IsValidResourcesPath — make the containsResources guard unsatisfiable → RED
		// ("Assets/Configs/Data" is accepted). 2026-08-02
		public void IsValidResourcesPath_NoResourcesSegment_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Assets/Configs/Data", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		// ADMIT: VersioningEditorSettings.IsValidResourcesPath accepts the shipped DefaultFolderPath.
		// RCR: VersioningEditorSettings.cs IsValidResourcesPath — return false from the success path → RED (the default
		// path is reported invalid). 2026-08-02
		public void IsValidResourcesPath_ValidDefaultPath_ReturnsTrue()
		{
			Assert.IsTrue(VersioningEditorSettings.IsValidResourcesPath(
				VersioningEditorSettings.DefaultFolderPath, out var error));
			Assert.IsNull(error);
		}
	}
}
