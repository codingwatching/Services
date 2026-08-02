using System.Collections;
using System.Reflection;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	/// <summary>
	/// Integration tests for <see cref="VersionServices"/> that exercise the async resource-loading
	/// pipeline. The post-load property-accessor assertions and the auto-load-on-access assertion are
	/// covered by the EditMode sync fixture (<see cref="VersionServicesSyncLoadTest"/>) via the
	/// synchronous <see cref="VersionServices.LoadVersionData"/> path — this fixture is kept narrowly
	/// scoped to the one thing that is genuinely distinct production code: the async load path.
	/// Requires Assets/Configs/Resources/version-data.txt to exist in the project.
	/// </summary>
	public class VersionServicesIntegrationTest
	{
		private static readonly FieldInfo LoadedField =
			typeof(VersionServices).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);

		[SetUp]
		public void ResetStaticState()
		{
			LoadedField.SetValue(null, false);
		}

		[UnityTest]
		// ADMIT: VersionServices.LoadVersionDataAsync is the only async load path and no other test exercises it;
		// broken TaskCompletionSource wiring would leave it never completing or never flipping the loaded flag.
		// RCR: VersionServices.cs LoadVersionDataAsync — comment out
		// `ApplyTextAsset(textAsset, asyncContext: true);` → RED (_loaded stays false). 2026-07-31
		public IEnumerator LoadVersionDataAsync_Successfully()
		{
			var task = VersionServices.LoadVersionDataAsync();

			while (!task.IsCompleted)
			{
				yield return null;
			}

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded");
		}
	}
}
