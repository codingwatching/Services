using GameLovers.Services.Editor.Explorer;
using GameLovers.Services.Editor.Explorer.Tabs;
using UnityEditor;

namespace GameLovers.Services.Versioning.Editor
{
	/// <summary>
	/// Top-bar menu stubs for the versioning pipeline under <c>Tools &gt; GameLovers &gt; Versioning</c>.
	/// Configuration lives in the Services Explorer Versioning tab.
	/// </summary>
	internal static class VersioningMenu
	{
		/// <summary>
		/// Regenerates <c>version-data.txt</c> from the current git state (non-store build).
		/// Equivalent to the domain-reload trigger — useful after branch switches without a full reload.
		/// </summary>
		[MenuItem("Tools/GameLovers/Versioning/Refresh Version Data", priority = 100)]
		private static void Refresh() => VersionEditorUtils.SetAndSaveInternalVersion(false);

		[MenuItem("Tools/GameLovers/Versioning/Open in Explorer", priority = 200)]
		private static void Open() => ServicesExplorerWindow.OpenOnTab<VersioningTab>();
	}
}
