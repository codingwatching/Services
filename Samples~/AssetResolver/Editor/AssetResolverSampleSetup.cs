using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameLovers.Services.AssetsImporter;
using GameLovers.Services.Editor.Explorer;
using GameLovers.Services.Editor.Explorer.Tabs;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.AssetResolver.Editor
{
	/// <summary>
	/// SAMPLE-ONLY editor automation for the AssetResolver sample. Eliminates the manual
	/// "mark sprites Addressable + populate <c>SpriteConfigs.asset</c>" steps that the per-sample
	/// README documents as a fallback.
	/// </summary>
	/// <remarks>
	/// <para>Triggered by:</para>
	/// <list type="bullet">
	///   <item><description><see cref="AssetResolverSampleAssetPostprocessor"/> when sprites are added to the sample's <c>Sprites/</c> folder.</description></item>
	///   <item><description>Menu: <c>Tools &gt; GameLovers &gt; Samples &gt; Asset Resolver &gt; Refresh Addressables</c>.</description></item>
	///   <item><description>Inspector button on this sample's <c>SpriteConfigs.asset</c> (added by the package's <c>AssetConfigsScriptableObjectEditor</c> only when the inspected asset lives under <c>Asset Resolver/</c>).</description></item>
	/// </list>
	/// <para>What it does (idempotent):</para>
	/// <list type="number">
	///   <item><description>Locates the sample root from this script's own asset path.</description></item>
	///   <item><description>For every <see cref="Sprite"/> under <c>Sprites/</c>, ensures the filename is one of <c>Hero/Coin/Enemy</c> (renames via substring match first, alphabetical fallback second).</description></item>
	///   <item><description>Ensures Addressables Settings exist (creates them if the user hasn't yet) and gets-or-creates a dedicated group <c>GameLoversServicesSamples_AssetResolver</c>.</description></item>
	///   <item><description>Marks each sprite Addressable in that group with a clean lowercase address.</description></item>
	///   <item><description>Populates the sample's <c>SpriteConfigs.asset</c> rows for the matching <c>SpriteId</c> values, never overwriting a user mapping that already points at a different sprite.</description></item>
	/// </list>
	/// <para>Runs entirely from this sample's editor assembly. If the user removes the sample, the
	/// automation goes with it — no orphan <c>InitializeOnLoad</c> in their project.</para>
	/// </remarks>
	public static class AssetResolverSampleSetup
	{
		/// <summary>
		/// Dedicated Addressables group for the sample's sprites. Never touches user-defined groups.
		/// Removing this group is the user's "undo".
		/// </summary>
		public const string GroupName = "GameLoversServicesSamples_AssetResolver";

		/// <summary>
		/// Addressables label applied to every sprite this sample marks Addressable.
		/// Lets the user demo the Services Explorer "Addressable Ids" tab against a
		/// sample-scoped label filter without needing to define their own labels first.
		/// Removing the sample removes the group (and the label entries with it).
		/// </summary>
		public const string LabelName = "services-sample-asset-resolver";

		private const string SpritesSubfolder = "Sprites";
		private const string SpriteConfigsFileName = "SpriteConfigs.asset";

		// Order matches SpriteId enum values: Hero=0, Coin=1, Enemy=2 (per Samples~/AssetResolver/SpriteId.cs).
		private static readonly string[] CanonicalNames = { "Hero", "Coin", "Enemy" };

		/// <summary>
		/// Manual entry point — exposed via menu. Logs a summary even when nothing changed.
		/// </summary>
		[MenuItem("Tools/GameLovers/Samples/Asset Resolver/Refresh Addressables")]
		public static void MenuRefresh()
		{
			RunSetup(silent: false);
		}

		/// <summary>
		/// Opens the Services Explorer focused on the Asset Resolver tab. Sample-scoped
		/// indirection used by <c>AssetResolverExample.Btn_OpenExplorer</c> so the sample's
		/// runtime assembly never has to take a reference on
		/// <c>GameLovers.Services.Editor</c> (per services package AGENTS.md §4 — same
		/// pattern as <c>AssetConfigsScriptableObjectEditor</c>'s sample-scoped refresh button).
		/// </summary>
		[MenuItem("Tools/GameLovers/Samples/Asset Resolver/Open in Explorer")]
		public static void MenuOpenInExplorer()
		{
			ServicesExplorerWindow.OpenOnTab<AssetResolverTab>();
		}

		/// <summary>
		/// Auto-trigger entry point. Suppresses logs unless something actually changed,
		/// to avoid console spam on every domain reload / asset import.
		/// </summary>
		internal static void RunSilent()
		{
			RunSetup(silent: true);
		}

		/// <summary>
		/// Safety net for the chicken-and-egg of UPM sample import: when the user first imports
		/// the sample, the sprites + this script land in the same import batch, but
		/// <see cref="AssetResolverSampleAssetPostprocessor.OnPostprocessAllAssets"/> only fires
		/// for that batch BEFORE this script compiles — so the post-processor misses the first
		/// import. <see cref="InitializeOnLoadMethodAttribute"/> runs on every domain reload after
		/// compile, defers via <see cref="EditorApplication.delayCall"/>, and short-circuits silently
		/// once the sample is already wired (idempotent — repeat reloads are no-ops).
		/// </summary>
		[InitializeOnLoadMethod]
		private static void OnDomainReload()
		{
			EditorApplication.delayCall += RunSilent;
		}

		private static void RunSetup(bool silent)
		{
			var sampleRoot = FindSampleRoot();
			if (sampleRoot == null)
			{
				if (!silent)
				{
					Debug.LogWarning("[AssetResolverSample] Could not locate sample root. " +
						"Re-import the sample via Package Manager.");
				}
				return;
			}

			var spritesDir = $"{sampleRoot}/{SpritesSubfolder}";
			if (!AssetDatabase.IsValidFolder(spritesDir))
			{
				if (!silent)
				{
					Debug.Log($"[AssetResolverSample] No Sprites/ folder at '{spritesDir}'. " +
						"Drop 3 PNGs there (any names; will auto-rename to Hero/Coin/Enemy) and re-run.");
				}
				return;
			}

			var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { spritesDir });
			if (spriteGuids.Length == 0)
			{
				if (!silent)
				{
					Debug.Log($"[AssetResolverSample] No sprites in '{spritesDir}'. " +
						"Drop 3 PNGs there and they'll be auto-marked Addressable + wired into SpriteConfigs.");
				}
				return;
			}

			var configsAsset = FindSpriteConfigs(sampleRoot);
			if (configsAsset == null)
			{
				if (!silent)
				{
					Debug.LogWarning($"[AssetResolverSample] '{SpriteConfigsFileName}' not found in '{sampleRoot}'.");
				}
				return;
			}

			var renamed = 0;
			try
			{
				AssetDatabase.StartAssetEditing();
				renamed = RenameToCanonical(spriteGuids, CanonicalNames);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}

			if (renamed > 0)
			{
				AssetDatabase.Refresh();
				spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { spritesDir });
			}

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(create: true);
			if (settings == null)
			{
				if (!silent)
				{
					Debug.LogWarning("[AssetResolverSample] Could not get/create Addressables settings.");
				}
				return;
			}

			var group = settings.FindGroup(GroupName);
			if (group == null)
			{
				group = settings.CreateGroup(
					GroupName,
					setAsDefaultGroup: false,
					readOnly: false,
					postEvent: false,
					schemasToCopy: null,
					typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
			}

			// Idempotent — AddLabel is a no-op when the label already exists in the project.
			settings.AddLabel(LabelName, postEvent: false);

			var nameToGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var sg in spriteGuids)
			{
				var p = AssetDatabase.GUIDToAssetPath(sg);
				var n = Path.GetFileNameWithoutExtension(p);
				nameToGuid[n] = sg;

				var entry = settings.CreateOrMoveEntry(sg, group, readOnly: false, postEvent: false);
				if (entry != null)
				{
					var desiredAddress = n.ToLowerInvariant();
					if (entry.address != desiredAddress)
					{
						entry.SetAddress(desiredAddress, postEvent: false);
					}
					// SetLabel(label, value: true, force: true) ensures the label is added even if
					// the label registry hasn't propagated yet (force bypasses the registry check).
					entry.SetLabel(LabelName, true, force: true, postEvent: false);
				}
			}

			var changes = WireSpriteConfigs(configsAsset, nameToGuid);

			if (!silent || changes > 0 || renamed > 0)
			{
				Debug.Log($"[AssetResolverSample] Setup complete. Group: '{GroupName}', " +
					$"label: '{LabelName}', sprites in group: {spriteGuids.Length}, " +
					$"configs entries set: {changes}, renamed: {renamed}.");
			}
		}

		// ---------------- Sample-root location ----------------

		private static string FindSampleRoot()
		{
			var guids = AssetDatabase.FindAssets($"t:MonoScript {nameof(AssetResolverSampleSetup)}");
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
				if (script != null && script.GetClass() == typeof(AssetResolverSampleSetup))
				{
					// path = ".../Asset Resolver/Editor/AssetResolverSampleSetup.cs"
					var editorDir = Path.GetDirectoryName(path);
					if (string.IsNullOrEmpty(editorDir))
					{
						continue;
					}

					return Path.GetDirectoryName(editorDir)?.Replace('\\', '/');
				}
			}
			return null;
		}

		private static AssetConfigsScriptableObject FindSpriteConfigs(string sampleRoot)
		{
			var assetPath = $"{sampleRoot}/{SpriteConfigsFileName}";
			var direct = AssetDatabase.LoadAssetAtPath<AssetConfigsScriptableObject>(assetPath);
			if (direct != null)
			{
				return direct;
			}

			var guids = AssetDatabase.FindAssets("t:AssetConfigsScriptableObject", new[] { sampleRoot });
			foreach (var g in guids)
			{
				var p = AssetDatabase.GUIDToAssetPath(g);
				var dir = Path.GetDirectoryName(p)?.Replace('\\', '/');
				if (dir == sampleRoot)
				{
					return AssetDatabase.LoadAssetAtPath<AssetConfigsScriptableObject>(p);
				}
			}
			return null;
		}

		// ---------------- Renaming ----------------

		private static int RenameToCanonical(string[] spriteGuids, string[] expected)
		{
			var renamed = 0;
			var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var pending = new List<string>();

			foreach (var g in spriteGuids)
			{
				var p = AssetDatabase.GUIDToAssetPath(g);
				var n = Path.GetFileNameWithoutExtension(p);
				if (Array.Exists(expected, e => string.Equals(e, n, StringComparison.OrdinalIgnoreCase)))
				{
					taken.Add(n);
				}
				else
				{
					pending.Add(g);
				}
			}

			var available = expected.Where(e => !taken.Contains(e)).ToList();

			// Pass 1: substring match — filename contains an expected name (case-insensitive).
			var stillPending = new List<string>();
			foreach (var g in pending)
			{
				var p = AssetDatabase.GUIDToAssetPath(g);
				var n = Path.GetFileNameWithoutExtension(p);
				var match = available.FirstOrDefault(e =>
					n.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0);

				if (match != null && TryRename(p, match))
				{
					taken.Add(match);
					available.Remove(match);
					renamed++;
				}
				else
				{
					stillPending.Add(g);
				}
			}

			// Pass 2: alphabetical fallback for anything still unmapped.
			stillPending.Sort((a, b) => string.Compare(
				AssetDatabase.GUIDToAssetPath(a),
				AssetDatabase.GUIDToAssetPath(b),
				StringComparison.Ordinal));

			foreach (var g in stillPending)
			{
				if (available.Count == 0)
				{
					break;
				}
				var p = AssetDatabase.GUIDToAssetPath(g);
				var match = available[0];
				if (TryRename(p, match))
				{
					taken.Add(match);
					available.RemoveAt(0);
					renamed++;
				}
			}

			return renamed;
		}

		private static bool TryRename(string path, string newName)
		{
			var error = AssetDatabase.RenameAsset(path, newName);
			if (string.IsNullOrEmpty(error))
			{
				return true;
			}
			Debug.LogWarning($"[AssetResolverSample] Rename '{path}' → '{newName}' failed: {error}");
			return false;
		}

		// ---------------- SpriteConfigs wiring ----------------

		private static int WireSpriteConfigs(
			AssetConfigsScriptableObject configsAsset,
			Dictionary<string, string> nameToGuid)
		{
			var so = new SerializedObject(configsAsset);
			var configsProp = so.FindProperty("_configs");
			if (configsProp == null || !configsProp.isArray)
			{
				return 0;
			}

			var changes = 0;

			for (var i = 0; i < CanonicalNames.Length; i++)
			{
				var canonical = CanonicalNames[i];
				if (!nameToGuid.TryGetValue(canonical, out var spriteGuid))
				{
					continue;
				}

				var idx = FindOrInsertEntryWithKey(configsProp, i);
				var element = configsProp.GetArrayElementAtIndex(idx);
				var keyProp = element.FindPropertyRelative("Key");
				var valueProp = element.FindPropertyRelative("Value");
				var guidProp = valueProp?.FindPropertyRelative("m_AssetGUID");

				if (keyProp == null || guidProp == null)
				{
					continue;
				}

				if (keyProp.intValue != i)
				{
					keyProp.intValue = i;
				}

				var existing = guidProp.stringValue;
				if (!string.IsNullOrEmpty(existing) && existing != spriteGuid)
				{
					// Respect a user mapping that points at a different sprite.
					continue;
				}

				if (existing != spriteGuid)
				{
					guidProp.stringValue = spriteGuid;
					changes++;
				}

				var subProp = valueProp.FindPropertyRelative("m_SubObjectName");
				if (subProp != null && !string.IsNullOrEmpty(subProp.stringValue))
				{
					subProp.stringValue = string.Empty;
				}
				var typeProp = valueProp.FindPropertyRelative("m_SubObjectType");
				if (typeProp != null && !string.IsNullOrEmpty(typeProp.stringValue))
				{
					typeProp.stringValue = string.Empty;
				}
			}

			if (changes > 0)
			{
				so.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(configsAsset);
				AssetDatabase.SaveAssetIfDirty(configsAsset);
			}

			return changes;
		}

		private static int FindOrInsertEntryWithKey(SerializedProperty configsProp, int targetKey)
		{
			for (var j = 0; j < configsProp.arraySize; j++)
			{
				var el = configsProp.GetArrayElementAtIndex(j);
				var keyProp = el.FindPropertyRelative("Key");
				if (keyProp != null && keyProp.intValue == targetKey)
				{
					return j;
				}
			}

			configsProp.InsertArrayElementAtIndex(configsProp.arraySize);
			var idx = configsProp.arraySize - 1;

			// Clear inserted slot — InsertArrayElementAtIndex copies the previous element.
			var inserted = configsProp.GetArrayElementAtIndex(idx);
			var insertedKey = inserted.FindPropertyRelative("Key");
			if (insertedKey != null)
			{
				insertedKey.intValue = targetKey;
			}
			var insertedValue = inserted.FindPropertyRelative("Value");
			var insertedGuid = insertedValue?.FindPropertyRelative("m_AssetGUID");
			if (insertedGuid != null)
			{
				insertedGuid.stringValue = string.Empty;
			}
			return idx;
		}
	}

	/// <summary>
	/// Listens for asset imports and triggers <see cref="AssetResolverSampleSetup.RunSilent"/>
	/// when a sprite (or any asset) lands under this sample's <c>Sprites/</c> folder.
	/// </summary>
	/// <remarks>
	/// <para>Defers the actual work to <see cref="EditorApplication.delayCall"/> — modifying assets
	/// during <c>OnPostprocessAllAssets</c> is unsafe; the delay pushes execution to the next
	/// editor tick when the asset database is in a consistent state.</para>
	///
	/// <para>This post-processor does NOT and CANNOT detect sample removal via <c>deletedAssets</c>.
	/// When the user deletes the sample folder via the Project window, Unity recompiles BEFORE
	/// firing <c>OnPostprocessAllAssets</c> for the deletion batch — by then this very class no
	/// longer exists in the recompiled assembly, and the callback is never delivered. Sample
	/// cleanup (removing the dedicated Addressables group + label) is therefore the user's
	/// responsibility, as documented in the per-sample README.</para>
	/// </remarks>
	internal sealed class AssetResolverSampleAssetPostprocessor : AssetPostprocessor
	{
		private const string MarkerSegment = "/Asset Resolver/Sprites/";

		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			foreach (var p in importedAssets)
			{
				if (p.IndexOf(MarkerSegment, StringComparison.Ordinal) >= 0)
				{
					EditorApplication.delayCall += AssetResolverSampleSetup.RunSilent;
					return;
				}
			}
		}
	}
}
