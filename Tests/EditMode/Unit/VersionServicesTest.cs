using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class VersionServicesTest
	{
		[Test]
		// ADMIT: VersionServices.FormatInternalVersion appends the build type suffix when one is present.
		// RCR: VersionServices.cs FormatInternalVersion — drop the build-type append → RED (the result no longer contains
		// "debug"). 2026-08-02
		public void FormatInternalVersion_WithBuildType_IncludesBuildType()
		{
			var data = new VersionServices.VersionData
			{
				CommitHash = "abc",
				BranchName = "main",
				BuildType = "debug",
				BuildNumber = "1"
			};
			var result = VersionServices.FormatInternalVersion(data);

			Assert.IsTrue(result.Contains("debug"));
			Assert.IsTrue(result.Contains("abc"));
			Assert.IsTrue(result.Contains("main"));
			Assert.IsTrue(result.Contains("1"));
		}

		[Test]
		// ADMIT: VersionServices.FormatInternalVersion omits the suffix entirely when BuildType is empty, so the string
		// never ends in a bare dot.
		// RCR: VersionServices.cs FormatInternalVersion — drop the emptiness guard → RED (the result ends with ".").
		// 2026-08-02
		public void FormatInternalVersion_WithoutBuildType_OmitsBuildType()
		{
			var data = new VersionServices.VersionData
			{
				CommitHash = "abc",
				BranchName = "main",
				BuildType = "",
				BuildNumber = "1"
			};
			var result = VersionServices.FormatInternalVersion(data);

			Assert.IsFalse(result.EndsWith("."));
			Assert.IsTrue(result.Contains("abc"));
		}
	}
}
