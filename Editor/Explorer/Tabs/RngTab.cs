using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLovers.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays the current state of <see cref="IRngService"/>: Seed, Counter, and a configurable
	/// peek-next preview. Supports Restore(count).
	/// </summary>
	public class RngTab : ServiceTab
	{
		public override string DisplayName => "RNG";
		protected override int RefreshIntervalMs => 500;

		private Label _seedLabel;
		private Label _counterLabel;
		private VisualElement _peekList;
		private IntegerField _peekCountField;
		private IntegerField _restoreCountField;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("State"));

			var seedRow = new VisualElement();
			seedRow.AddToClassList("row");
			seedRow.Add(new Label("Seed") { style = { width = 120 } });
			_seedLabel = new Label("—");
			_seedLabel.AddToClassList("row-value");
			seedRow.Add(_seedLabel);
			scroll.Add(seedRow);

			var counterRow = new VisualElement();
			counterRow.AddToClassList("row");
			counterRow.Add(new Label("Counter") { style = { width = 120 } });
			_counterLabel = new Label("—");
			_counterLabel.AddToClassList("row-value");
			counterRow.Add(_counterLabel);
			scroll.Add(counterRow);

			scroll.Add(MakeSectionLabel("Peek Next N Values"));

			var peekControls = new VisualElement();
			peekControls.style.flexDirection = FlexDirection.Row;
			peekControls.style.alignItems = Align.Center;
			peekControls.style.marginBottom = 4;

			_peekCountField = new IntegerField("Count") { value = 5 };
			_peekCountField.style.width = 130;
			peekControls.Add(_peekCountField);

			var peekBtn = new Button(OnPeek) { text = "Peek" };
			peekBtn.AddToClassList("row-btn");
			peekControls.Add(peekBtn);

			scroll.Add(peekControls);

			_peekList = new VisualElement();
			scroll.Add(_peekList);

			scroll.Add(MakeSectionLabel("Restore"));

			var restoreRow = new VisualElement();
			restoreRow.style.flexDirection = FlexDirection.Row;
			restoreRow.style.alignItems = Align.Center;

			_restoreCountField = new IntegerField("To count") { value = 0 };
			_restoreCountField.style.width = 130;
			restoreRow.Add(_restoreCountField);

			var restoreBtn = new Button(OnRestore) { text = "Restore" };
			restoreBtn.AddToClassList("row-btn");
			restoreRow.Add(restoreBtn);

			scroll.Add(restoreRow);
			Add(scroll);
		}

		protected override void Refresh()
		{
			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				_seedLabel.text = "not bound";
				_counterLabel.text = "";
				_peekList.Clear();
				return;
			}

			_seedLabel.text = rng.Data.Seed.ToString();
			_counterLabel.text = rng.Counter.ToString();
		}

		private void OnPeek()
		{
			_peekList.Clear();

			var rng = TryResolve<IRngService>() as RngService;

			if (rng == null)
			{
				_peekList.Add(MakeEmptyLabel("IRngService not bound"));
				return;
			}

			var count = Mathf.Clamp(_peekCountField.value, 1, 50);
			var stateCopy = RngService.CopyRngState(((RngData)rng.Data).State);

			for (var i = 0; i < count; i++)
			{
				var val = RngService.Range(0, int.MaxValue, stateCopy, false);
				var row = MakeRow($"[{i}]", val.ToString());
				_peekList.Add(row);
			}
		}

		private void OnRestore()
		{
			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				return;
			}

			rng.Restore(_restoreCountField.value);
			Refresh();
		}
	}
}
