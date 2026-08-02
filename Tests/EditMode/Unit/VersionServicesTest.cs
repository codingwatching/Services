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

			Assert.That(result.Contains("debug"), Is.True);
			Assert.That(result.Contains("abc"), Is.True);
			Assert.That(result.Contains("main"), Is.True);
			Assert.That(result.Contains("1"), Is.True);
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

			Assert.That(result.EndsWith("."), Is.False);
			Assert.That(result.Contains("abc"), Is.True);
		}
	}
}
