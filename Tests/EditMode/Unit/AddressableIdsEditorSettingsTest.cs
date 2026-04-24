using GameLovers.Services.AddressableIds.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AddressableIdsEditorSettingsTest
	{
		// ---- IsValidIdentifier ----

		[Test]
		public void IsValidIdentifier_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_WhitespaceOnly_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("   ", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_StartsWithDigit_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("1AddressableId", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ContainsDot_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("Addressable.Id", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ContainsHyphen_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("Addressable-Id", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ValidDefault_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidIdentifier("AddressableId", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidIdentifier_UnderscorePrefix_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidIdentifier("_AddressableId", out var error));
			Assert.IsNull(error);
		}

		// ---- IsValidNamespace ----

		[Test]
		public void IsValidNamespace_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_WhitespaceOnly_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("   ", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_TrailingDot_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game.Ids.", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_ConsecutiveDots_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game..Ids", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_SegmentStartsWithDigit_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game.1Ids", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_ValidDefault_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Game.Ids", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidNamespace_SingleSegment_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Game", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidNamespace_DeepHierarchy_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Com.GameLovers.Game.Ids", out var error));
			Assert.IsNull(error);
		}
	}
}
