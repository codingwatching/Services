using UnityEngine.UIElements;

namespace GameLovers.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays active <see cref="IAsyncCoroutine"/> handles tracked by <see cref="ICoroutineService"/>.
	/// Editor-build tracking is enabled via <c>#if UNITY_EDITOR</c> in <see cref="CoroutineService"/>.
	/// </summary>
	public class CoroutineTab : ServiceTab
	{
		public override string DisplayName => "Coroutine";

		private ScrollView _scroll;
		private VisualElement _list;
		private Label _totalLabel;

		protected override void BuildUi()
		{
			var header = new VisualElement();
			header.AddToClassList("tab-header-row");
			_totalLabel = new Label("Active: 0");
			_totalLabel.AddToClassList("tab-section-label");
			header.Add(_totalLabel);
			Add(header);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.AddToClassList("tab-scroll");
			_list = new VisualElement();
			_scroll.Add(_list);
			Add(_scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Stop All Coroutines", OnStopAll));
		Add(bar);
		}

		protected override void Refresh()
		{
			_list.Clear();

#if UNITY_EDITOR
			var cs = TryResolve<ICoroutineService>() as CoroutineService;

			if (cs == null)
			{
				_totalLabel.text = "ICoroutineService not bound";
				_list.Add(MakeEmptyLabel("ICoroutineService not bound"));
				return;
			}

			var active = cs.ActiveAsyncCoroutines;
			_totalLabel.text = $"Active: {active.Count}";

			if (active.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var co in active)
			{
				var status = co.IsRunning ? "running" : (co.IsCompleted ? "completed" : "stopped");
				var row = MakeRow($"t={co.StartTime:F2}s", $"[{status}]");
				var stopBtn = MakeRowButton("Stop", () =>
				{
					co.StopCoroutine(true);
					Refresh();
				}, danger: true);
				row.Add(stopBtn);
				_list.Add(row);
			}
#else
			_totalLabel.text = "Editor tracking not available in non-editor builds";
#endif
		}

		private void OnStopAll()
		{
			var cs = TryResolve<ICoroutineService>();
			cs?.StopAllCoroutines();
			Refresh();
		}
	}
}
