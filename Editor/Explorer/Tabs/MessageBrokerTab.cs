using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLovers.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays all <see cref="IMessage"/> subscriptions held by <see cref="IMessageBrokerService"/>.
	/// Supports Unsubscribe per message type, UnsubscribeAll, and a test-publish action.
	/// </summary>
	public class MessageBrokerTab : ServiceTab
	{
		public override string DisplayName => "Message Broker";

		private ScrollView _scroll;
		private VisualElement _list;
		private TextField _publishTypeField;
		private Label _publishStatus;

		protected override void BuildUi()
		{
			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.AddToClassList("tab-scroll");
			_list = new VisualElement();
			_scroll.Add(_list);
			Add(_scroll);

			var bar = MakeActionBar();

			var publishRow = new VisualElement();
			publishRow.style.flexDirection = FlexDirection.Row;
			publishRow.style.alignItems = Align.Center;
			publishRow.style.flexWrap = Wrap.Wrap;
			publishRow.style.marginTop = 4;

			_publishTypeField = new TextField { label = "Type (FQN)", value = "" };
			_publishTypeField.style.flexGrow = 1;
			_publishTypeField.style.minWidth = 200;
			publishRow.Add(_publishTypeField);

			var publishBtn = new Button(OnPublishTest) { text = "Publish default(T)" };
			publishBtn.AddToClassList("row-btn");
			publishRow.Add(publishBtn);

			_publishStatus = new Label();
			_publishStatus.style.fontSize = 10;
			_publishStatus.style.marginLeft = 6;
			_publishStatus.style.color = new StyleColor(new Color(0.5f, 0.9f, 0.5f));
			publishRow.Add(_publishStatus);

			bar.Add(publishRow);

		bar.Add(MakePrimaryButton("Unsubscribe All", OnUnsubscribeAll));

			Add(bar);
		}

		protected override void Refresh()
		{
			_list.Clear();

			var broker = TryResolve<IMessageBrokerService>() as MessageBrokerService;

			if (broker == null)
			{
				_list.Add(MakeEmptyLabel("IMessageBrokerService not bound"));
				return;
			}

			var subs = broker.Subscriptions;

			if (subs.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var kvp in subs)
			{
				var messageType = kvp.Key;
				var subscribers = kvp.Value;

				var foldout = new Foldout { text = $"{messageType.Name}  ({subscribers.Count})" };
				foldout.AddToClassList("section-foldout");

				foreach (var sub in subscribers)
				{
					var targetName = sub.Key?.GetType().Name ?? "(null)";
					var methodName = (sub.Value as Delegate)?.Method?.Name ?? "?";
					var subRow = MakeRow($"  {targetName}", methodName);
					foldout.Add(subRow);
				}

				var unsubBtn = MakeRowButton("Unsubscribe<T>(null)", () => OnUnsubscribeType(broker, messageType), danger: true);
				foldout.Add(unsubBtn);

				_list.Add(foldout);
			}
		}

		private void OnUnsubscribeType(MessageBrokerService broker, Type messageType)
		{
			if (!EditorUtility.DisplayDialog("Unsubscribe",
				$"Remove ALL subscribers for {messageType.Name}?", "Remove", "Cancel"))
			{
				return;
			}

			var method = typeof(IMessageBrokerService)
				.GetMethod(nameof(IMessageBrokerService.Unsubscribe))
				?.MakeGenericMethod(messageType);
			method?.Invoke(broker, new object[] { null });
			Refresh();
		}

		private void OnUnsubscribeAll()
		{
			var broker = TryResolve<IMessageBrokerService>();

			if (broker == null)
			{
				return;
			}

			if (!EditorUtility.DisplayDialog("UnsubscribeAll",
				"Remove ALL subscriptions from the message broker?", "Remove All", "Cancel"))
			{
				return;
			}

			broker.UnsubscribeAll(null);
			Refresh();
		}

		private void OnPublishTest()
		{
			var broker = TryResolve<IMessageBrokerService>();
			_publishStatus.text = "";

			if (broker == null)
			{
				_publishStatus.text = "not bound";
				return;
			}

			var typeName = _publishTypeField.value?.Trim();

			if (string.IsNullOrEmpty(typeName))
			{
				_publishStatus.text = "enter a type name";
				return;
			}

			var type = AppDomain.CurrentDomain
				.GetAssemblies()
				.Select(a => a.GetType(typeName, throwOnError: false))
				.FirstOrDefault(t => t != null);

			if (type == null)
			{
				_publishStatus.text = $"type not found";
				return;
			}

			if (!typeof(IMessage).IsAssignableFrom(type))
			{
				_publishStatus.text = "not an IMessage";
				return;
			}

			try
			{
				var msg = Activator.CreateInstance(type);
				var publishMethod = typeof(IMessageBrokerService)
					.GetMethod(nameof(IMessageBrokerService.Publish))
					?.MakeGenericMethod(type);
				publishMethod?.Invoke(broker, new[] { msg });
				_publishStatus.text = "published";
			}
			catch (Exception ex)
			{
				_publishStatus.text = "error";
				Debug.LogError($"[ServicesExplorer] Publish test threw: {ex.Message}");
			}
		}
	}
}
