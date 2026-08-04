using System.Reflection;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	/// <summary>
	/// EditMode unit coverage for <see cref="VersionServices.LoadVersionData"/> (synchronous).
	/// The async sibling is covered in PlayMode/Integration/VersionServicesIntegrationTest.cs;
	/// the sync path needs no Unity runtime, so it lives here. Requires
	/// Assets/Configs/Resources/version-data.txt to exist in the host project (written
	/// automatically by VersionEditorUtils on every domain reload).
	/// </summary>
	[TestFixture]
	public class VersionServicesSyncLoadTest
	{
		private static readonly FieldInfo LoadedField =
			typeof(VersionServices).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);

		[SetUp]
		public void ResetStaticState()
		{
			LoadedField.SetValue(null, false);
		}

		[Test]
		// ADMIT: VersionServices.EnsureLoaded lazy-loads the version resource on first property access when Bootstrap has
		// not yet run.
		// RCR: VersionServices.cs EnsureLoaded — drop the LoadVersionData() fallback → RED (_loaded stays false after
		// reading all four accessors). 2026-08-02
		public void AccessBeforeLoad_AutoLoads()
		{
			Assert.IsFalse((bool)LoadedField.GetValue(null), "Precondition: SetUp resets _loaded to false");

			Assert.DoesNotThrow(() => { var _ = VersionServices.VersionInternal; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Branch; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Commit; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.BuildNumber; });

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Accessor should auto-trigger LoadVersionData via EnsureLoaded");
		}

		[Test]
		// ADMIT: VersionServices.ApplyTextAsset flips _loaded once the version-data TextAsset has parsed.
		// RCR: VersionServices.cs ApplyTextAsset — leave _loaded false on the success path → RED (the flag never flips).
		// Broad: also reddens the auto-load and post-load accessor tests. 2026-08-02
		public void LoadVersionData_Successfully_FlipsLoadedFlag()
		{
			VersionServices.LoadVersionData();

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded after sync call");
		}

		[Test]
		public void LoadVersionData_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => VersionServices.LoadVersionData());
		}

		[Test]
		// ADMIT: VersionServices.VersionInternal formats the loaded VersionData rather than returning a bare fallback.
		// RCR: VersionServices.cs VersionInternal — return string.Empty on the loaded branch → RED (Assert.IsNotEmpty
		// fails). 2026-08-02
		public void AfterLoad_VersionInternal_ContainsExpectedParts()
		{
			VersionServices.LoadVersionData();

			var version = VersionServices.VersionInternal;

			Assert.IsNotNull(version);
			Assert.IsNotEmpty(version);
			Assert.IsTrue(version.Contains("."), "VersionInternal should contain version separators");
		}

		[Test]
		// ADMIT: VersionServices.Branch surfaces the loaded VersionData.BranchName instead of the not-loaded fallback.
		// RCR: VersionServices.cs Branch — always return string.Empty → RED (Assert.IsNotEmpty fails). A6: assumes the
		// host project's version-data.txt carries a non-empty branch. 2026-08-02
		public void AfterLoad_Branch_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var branch = VersionServices.Branch;

			Assert.IsNotNull(branch);
			Assert.IsNotEmpty(branch);
		}

		[Test]
		// ADMIT: VersionServices.Commit surfaces the loaded VersionData.CommitHash instead of the not-loaded fallback.
		// RCR: VersionServices.cs Commit — always return string.Empty → RED (Assert.IsNotEmpty fails). A6: assumes the
		// host project's version-data.txt carries a non-empty commit. 2026-08-02
		public void AfterLoad_Commit_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var commit = VersionServices.Commit;

			Assert.IsNotNull(commit);
			Assert.IsNotEmpty(commit);
		}

		[Test]
		// ADMIT: VersionServices.BuildNumber surfaces the loaded VersionData.BuildNumber instead of the not-loaded
		// fallback.
		// RCR: VersionServices.cs BuildNumber — always return string.Empty → RED (Assert.IsNotEmpty fails). A6: assumes
		// the host project's version-data.txt carries a non-empty build number. 2026-08-02
		public void AfterLoad_BuildNumber_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var buildNumber = VersionServices.BuildNumber;

			Assert.IsNotNull(buildNumber);
			Assert.IsNotEmpty(buildNumber);
		}

		[Test]
		// ADMIT: VersionServices.VersionExternal must forward Application.version verbatim, with no dependency on the
		// version-data resource having been loaded.
		// RCR: VersionServices.cs VersionExternal — `=> string.Empty;` → RED (AreEqual reports the project version
		// against ""). Unique: VersionInternal has its own fallback path and its own tests. 2026-08-04
		public void VersionExternal_AlwaysAccessible_WithoutLoad()
		{
			Assert.AreEqual(Application.version, VersionServices.VersionExternal);
		}
	}
}
