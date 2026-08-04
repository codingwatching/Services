using GameLovers.Services.AssetsImporter.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AssetsImporterEditorSettingsTest
	{
		private bool _originalValue;

		[SetUp]
		public void Init()
		{
			_originalValue = AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh;
		}

		[TearDown]
		public void Cleanup()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = _originalValue;
		}

		[Test]
		// ADMIT: AssetsImporterEditorSettings.AutoUpdateOnRefresh's setter stores the caller's value on the backing field
		// before persisting.
		// RCR: AssetsImporterEditorSettings.cs AutoUpdateOnRefresh setter — hard-code the stored value to false → RED (the
		// getter never reports true). 2026-08-02
		public void AutoUpdateOnRefresh_SetterRoundTrips_PreservesValue()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = true;
			Assert.IsTrue(AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh);

			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = false;
			Assert.IsFalse(AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh);
		}
	}
}
