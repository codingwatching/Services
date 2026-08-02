using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class VersionServicesTest
	{
		[Test]
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
