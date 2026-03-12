using System;
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
	/// pipeline and all post-load property accessors.
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

		[UnityTest, Order(1)]
		public IEnumerator AccessBeforeLoad_Throws()
		{
			Assert.Throws<Exception>(() => { var _ = VersionServices.VersionInternal; });
			Assert.Throws<Exception>(() => { var _ = VersionServices.Branch; });
			Assert.Throws<Exception>(() => { var _ = VersionServices.Commit; });
			Assert.Throws<Exception>(() => { var _ = VersionServices.BuildNumber; });

			yield return null;
		}

		[UnityTest, Order(2)]
		public IEnumerator LoadVersionDataAsync_Successfully()
		{
			var task = VersionServices.LoadVersionDataAsync();

			while (!task.IsCompleted)
			{
				yield return null;
			}

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded");
		}

		[UnityTest, Order(3)]
		public IEnumerator AfterLoad_VersionInternal_ContainsExpectedParts()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var version = VersionServices.VersionInternal;

			Assert.IsNotNull(version);
			Assert.IsNotEmpty(version);
			Assert.IsTrue(version.Contains("."), "VersionInternal should contain version separators");
		}

		[UnityTest, Order(4)]
		public IEnumerator AfterLoad_Branch_ReturnsNonEmptyString()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var branch = VersionServices.Branch;

			Assert.IsNotNull(branch);
			Assert.IsNotEmpty(branch);
		}

		[UnityTest, Order(5)]
		public IEnumerator AfterLoad_Commit_ReturnsNonEmptyString()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var commit = VersionServices.Commit;

			Assert.IsNotNull(commit);
			Assert.IsNotEmpty(commit);
		}

		[UnityTest, Order(6)]
		public IEnumerator AfterLoad_BuildNumber_ReturnsNonEmptyString()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var buildNumber = VersionServices.BuildNumber;

			Assert.IsNotNull(buildNumber);
			Assert.IsNotEmpty(buildNumber);
		}

		[UnityTest, Order(7)]
		public IEnumerator VersionExternal_AlwaysAccessible_WithoutLoad()
		{
			var external = VersionServices.VersionExternal;

			Assert.IsNotNull(external);
			Assert.IsNotEmpty(external);

			yield return null;
		}
	}
}
