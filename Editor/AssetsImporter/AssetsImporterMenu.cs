using GameLovers.Services.Editor.Explorer;
using GameLovers.Services.Editor.Explorer.Tabs;
using UnityEditor;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.AssetsImporter.Editor
{
	/// <summary>
	/// Top-bar menu stubs for the Assets Importer pipeline under <c>Tools &gt; GameLovers &gt; Assets Importer</c>.
	/// Settings and per-importer controls live in the Services Explorer Assets Importer tab.
	/// </summary>
	internal static class AssetsImporterMenu
	{
		[MenuItem("Tools/GameLovers/Assets Importer/Import Assets Data", priority = 100)]
		private static void ImportAll() => AssetsImporterEditorUtils.ImportAll();

		[MenuItem("Tools/GameLovers/Assets Importer/Open in Explorer", priority = 200)]
		private static void Open() => ServicesExplorerWindow.OpenOnTab<AssetsImporterTab>();
	}
}
