using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLovers.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Abstract base for all Services Explorer tab panels.
	/// Handles play-mode-aware refresh scheduling and the "not in Play" snapshot banner.
	/// </summary>
	public abstract class ServiceTab : VisualElement
	{
		private const string BannerClass = "tab-banner";
		private const string RootClass = "tab-root";

		private Label _banner;
		private IVisualElementScheduledItem _refreshTask;
		private bool _wasPlaying;

		/// <summary>Tab header text shown in the TabView strip.</summary>
		public abstract string DisplayName { get; }

		/// <summary>Refresh interval in milliseconds during Play mode. Override for slower updates.</summary>
		protected virtual int RefreshIntervalMs => 250;

		protected ServiceTab()
		{
			AddToClassList(RootClass);
			style.flexGrow = 1;

			_banner = new Label("Not in Play mode — showing last snapshot");
			_banner.AddToClassList(BannerClass);
			Add(_banner);

			BuildUi();
			UpdateBannerVisibility();

			RegisterCallback<AttachToPanelEvent>(OnAttach);
			RegisterCallback<DetachFromPanelEvent>(OnDetach);
		}

		/// <summary>Build all child VisualElements. Called once in the constructor after the banner.</summary>
		protected abstract void BuildUi();

		/// <summary>
		/// Pull latest data from services and repopulate UI.
		/// Called every <see cref="RefreshIntervalMs"/> ms during Play mode,
		/// and once manually on attach (Edit mode snapshot).
		/// </summary>
		protected abstract void Refresh();

		private void OnAttach(AttachToPanelEvent _)
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			_wasPlaying = EditorApplication.isPlayingOrWillChangePlaymode;

			Refresh();

			if (EditorApplication.isPlaying)
			{
				StartRefreshTimer();
			}
		}

		private void OnDetach(DetachFromPanelEvent _)
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			StopRefreshTimer();
		}

		private void OnPlayModeChanged(PlayModeStateChange state)
		{
			switch (state)
			{
				case PlayModeStateChange.EnteredPlayMode:
					UpdateBannerVisibility();
					Refresh();
					StartRefreshTimer();
					break;
				case PlayModeStateChange.ExitingPlayMode:
					StopRefreshTimer();
					break;
				case PlayModeStateChange.EnteredEditMode:
					UpdateBannerVisibility();
					Refresh();
					break;
			}
		}

		private void StartRefreshTimer()
		{
			StopRefreshTimer();
			_refreshTask = schedule.Execute(() =>
			{
				if (panel != null)
				{
					Refresh();
				}
			}).Every(RefreshIntervalMs);
		}

		private void StopRefreshTimer()
		{
			_refreshTask?.Pause();
			_refreshTask = null;
		}

		private void UpdateBannerVisibility()
		{
			_banner.style.display = EditorApplication.isPlaying
				? DisplayStyle.None
				: DisplayStyle.Flex;
		}

		// ---- Helpers for sub-classes ----

		/// <summary>
		/// Creates a horizontal row with a type label on the left and optional value label,
		/// then appends action buttons on the right.
		/// </summary>
		protected static VisualElement MakeRow(string label, string value = null)
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			var lbl = new Label(label);
			lbl.AddToClassList("row-label");
			row.Add(lbl);

			if (value != null)
			{
				var val = new Label(value);
				val.AddToClassList("row-value");
				row.Add(val);
			}

			return row;
		}

		/// <summary>Creates a small action button styled for use inside a row.</summary>
		protected static Button MakeRowButton(string text, System.Action onClick, bool danger = false)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("row-btn");

			if (danger)
			{
				btn.AddToClassList("row-btn-danger");
			}

			return btn;
		}

		/// <summary>Displays a section heading label.</summary>
		protected static Label MakeSectionLabel(string text)
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-section-label");
			return lbl;
		}

		/// <summary>Displays an italic "empty" label.</summary>
		protected static Label MakeEmptyLabel(string text = "— none —")
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-empty-label");
			return lbl;
		}

	/// <summary>Creates a bottom action bar VisualElement.</summary>
	protected static VisualElement MakeActionBar()
	{
		var bar = new VisualElement();
		bar.AddToClassList("action-bar");
		return bar;
	}

	/// <summary>
	/// Creates a visually prominent primary action button styled with the <c>action-primary</c> USS class.
	/// Use for the single most important call-to-action in each tab's action bar.
	/// </summary>
	protected static Button MakePrimaryButton(string text, System.Action onClick)
	{
		var btn = new Button(onClick) { text = text };
		btn.AddToClassList("action-primary");
		return btn;
	}

		/// <summary>
		/// Tries to resolve <typeparamref name="T"/> from <see cref="MainInstaller"/>.
		/// Returns null and does not throw if the service is not bound.
		/// </summary>
		protected static T TryResolve<T>() where T : class
		{
			MainInstaller.TryResolve<T>(out var service);
			return service;
		}
	}
}
