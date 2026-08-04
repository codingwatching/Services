using System.Collections.Generic;
using NUnit.Framework;
using GameLovers.Services.AddressableIds.Editor;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class AddressableIdsGeneratorTest
	{
		[Test]
		// ADMIT: AddressableIdsGeneratorUtils.AppendAddressEnumMembers must append the disambiguated `name` from
		// ResolveSanitizedEnumName rather than re-deriving GetCleanName(address) — two addresses that sanitize to
		// the same identifier would otherwise emit duplicate enum members, a C# compile error.
		// RCR: AddressableIdsGeneratorUtils.cs AppendAddressEnumMembers — revert to
		// `stringBuilder.Append(GetCleanName(addresses[i], true));` → RED (both members collapse to
		// "ui_main_menu"). 2026-08-01
		public void GenerateEnumSource_TwoAddressesCollideAfterSanitization_ProducesDistinctMemberNames()
		{
			// This pair does not collide on GetCleanName's stripped-extension rule ("ui.main.menu" would clean
			// to "ui.main", not "ui_main_menu") — these two addresses differ only by which invalid-identifier
			// separator they use ('-' vs '/') and both genuinely clean to "ui_main_menu", exercising the
			// disambiguation fallback in ResolveSanitizedEnumName.
			var addresses = new List<string> { "ui/main-menu", "ui-main/menu" };

			var source = AddressableIdsGeneratorUtils.BuildEnumSource(addresses);
			var members = ParseMembers(source);

			Assert.AreEqual(2, members.Count);
			Assert.AreEqual("ui_main_menu", members[0]);
			Assert.AreNotEqual(members[0], members[1]); // the second collides, so it must take the "_<filetype>" suffix
			Assert.AreEqual("ui_main_menu_", members[1]); // neither address has a file extension, so filetype is empty
		}

		[Test]
		// ADMIT: AddressableIdsGeneratorUtils.AppendAddressEnumMembers iterates in input order with no reorder or
		// dedup step, so appending a new address must not renumber existing members' implicit enum positions.
		// RCR: AddressableIdsGeneratorUtils.cs AppendAddressEnumMembers — reverse the loop to
		// `for (var i = addresses.Count - 1; i >= 0; i--)` → RED ("a" is no longer the first member). 2026-08-01
		public void GenerateEnumSource_AddressAppendedToInput_DoesNotRenumberExistingMembers()
		{
			var firstCall = AddressableIdsGeneratorUtils.BuildEnumSource(new List<string> { "a", "b" });
			var secondCall = AddressableIdsGeneratorUtils.BuildEnumSource(new List<string> { "a", "b", "c" });

			var firstMembers = ParseMembers(firstCall);
			var secondMembers = ParseMembers(secondCall);

			Assert.AreEqual(2, firstMembers.Count);
			Assert.AreEqual(3, secondMembers.Count);

			// Position 0 and 1 carry the implicit enum values 0 and 1 (no explicit "= N" is emitted by the
			// generator); appending "c" must not shift "a"/"b" off their original positions.
			Assert.AreEqual(firstMembers[0], secondMembers[0]);
			Assert.AreEqual(firstMembers[1], secondMembers[1]);
			Assert.AreEqual("a", secondMembers[0]);
			Assert.AreEqual("b", secondMembers[1]);
		}

		[Test]
		// ADMIT: AddressableIdsGeneratorUtils.BuildEnumSource writes both enum-body braces unconditionally, so a
		// project with zero matching addresses still generates a syntactically valid empty enum.
		// RCR: AddressableIdsGeneratorUtils.cs BuildEnumSource — delete `stringBuilder.AppendLine("\t}");` → RED
		// (the result contains no closing brace). 2026-08-01
		public void GenerateEnumSource_EmptyAddressList_ProducesCompilableEmptyEnum()
		{
			var source = AddressableIdsGeneratorUtils.BuildEnumSource(new List<string>());

			Assert.IsTrue(source.Contains("{"));
			Assert.IsTrue(source.Contains("}"));
			Assert.IsFalse(source.Contains(","));
			Assert.AreEqual(0, ParseMembers(source).Count);
		}

		/// <summary>
		/// Splits a <see cref="AddressableIdsGeneratorUtils.BuildEnumSource"/> result into its member-name
		/// tokens, in declaration order, stripping the brace lines and trailing commas.
		/// </summary>
		private static List<string> ParseMembers(string enumSource)
		{
			var members = new List<string>();
			var lines = enumSource.Split('\n');

			foreach (var rawLine in lines)
			{
				var line = rawLine.Trim().TrimEnd(',').Trim();

				if (line.Length == 0 || line == "{" || line == "}")
				{
					continue;
				}

				members.Add(line);
			}

			return members;
		}
	}
}
