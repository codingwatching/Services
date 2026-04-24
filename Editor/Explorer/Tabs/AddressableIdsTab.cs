using System.IO;
using GameLovers.Services.AddressableIds.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLovers.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Services Explorer tab for the Addressable Ids Generator.
	/// Replaces the old <c>AddressablesIdGeneratorSettings.asset</c> custom inspector.
	/// Reads/writes settings via <see cref="AddressableIdsEditorSettings"/> and invokes
	/// generation via <see cref="AddressableIdsGeneratorUtils"/>.
	/// </summary>
	public class AddressableIdsTab : ServiceTab
	{
		public override string DisplayName => "Addressable Ids";
		protected override int RefreshIntervalMs => 2000;

		private TextField _filenameField;
		private Label _filenameError;
		private TextField _namespaceField;
		private Label _namespaceError;
		private TextField _labelField;
		private Label _outputPathLabel;
		private Label _outputStatusLabel;
		private Label _lastResultLabel;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			// ---- Generator Settings section ----
			scroll.Add(MakeSectionLabel("Generator Settings"));

			var settings = AddressableIdsEditorSettings.instance;

			// Script Filename
			_filenameField = new TextField("Script Filename")
			{
				tooltip = "Name of the generated C# file and the enum it contains (no extension).",
				value = settings.ScriptFilename
			};
			_filenameError = MakeInlineError();
			_filenameField.RegisterValueChangedCallback(e =>
			{
				if (AddressableIdsEditorSettings.IsValidIdentifier(e.newValue, out var err))
				{
					settings.ScriptFilename = e.newValue;
					_filenameError.style.display = DisplayStyle.None;
				}
				else
				{
					_filenameError.text = err;
					_filenameError.style.display = DisplayStyle.Flex;
				}

				RefreshOutput();
			});
			scroll.Add(_filenameField);
			scroll.Add(_filenameError);

			// Namespace
			_namespaceField = new TextField("Namespace")
			{
				tooltip = "C# namespace for the generated Addressable Ids file.",
				value = settings.Namespace
			};
			_namespaceError = MakeInlineError();
			_namespaceField.RegisterValueChangedCallback(e =>
			{
				if (AddressableIdsEditorSettings.IsValidNamespace(e.newValue, out var err))
				{
					settings.Namespace = e.newValue;
					_namespaceError.style.display = DisplayStyle.None;
				}
				else
				{
					_namespaceError.text = err;
					_namespaceError.style.display = DisplayStyle.Flex;
				}
			});
			scroll.Add(_namespaceField);
			scroll.Add(_namespaceError);

			// Addressable Label filter
			_labelField = new TextField("Addressable Label")
			{
				tooltip = "Label filter for Addressables asset entries. Leave empty to include all non-read-only groups.",
				value = settings.AddressableLabel
			};
			_labelField.RegisterValueChangedCallback(e =>
			{
				settings.AddressableLabel = e.newValue;
			});
			scroll.Add(_labelField);

			// ---- Output section ----
			scroll.Add(MakeSectionLabel("Output"));

			_outputStatusLabel = new Label();
			_outputStatusLabel.style.fontSize = 10;
			_outputStatusLabel.style.marginBottom = 2;
			scroll.Add(_outputStatusLabel);

			_outputPathLabel = new Label();
			_outputPathLabel.AddToClassList("json-preview");
			scroll.Add(_outputPathLabel);

			_lastResultLabel = new Label();
			_lastResultLabel.style.fontSize = 10;
			_lastResultLabel.style.color = new StyleColor(new Color(0.6f, 0.9f, 0.6f));
			_lastResultLabel.style.marginTop = 2;
			scroll.Add(_lastResultLabel);

			var bar = MakeActionBar();
			bar.Add(new Button(OnRevealFile) { text = "Reveal file" });
			scroll.Add(bar);

			Add(scroll);

			// ---- Bottom action bar ----
			var mainBar = MakeActionBar();
			mainBar.Add(MakePrimaryButton("Generate Addressable Ids", OnGenerate));
			mainBar.Add(new Button(OnOpenAddressablesGroups) { text = "Open Addressables Groups" });
			Add(mainBar);

			RefreshOutput();
		}

		protected override void Refresh()
		{
			var settings = AddressableIdsEditorSettings.instance;
			_filenameField.SetValueWithoutNotify(settings.ScriptFilename);
			_namespaceField.SetValueWithoutNotify(settings.Namespace);
			_labelField.SetValueWithoutNotify(settings.AddressableLabel);
			RefreshOutput();
		}

		private void RefreshOutput()
		{
			var settings = AddressableIdsEditorSettings.instance;
			var scriptPath = $"Assets/{settings.ScriptFilename}.cs";

			// Also search for an existing file with this name in case it's not at the root.
			var found = AssetDatabase.FindAssets($"t:Script {settings.ScriptFilename}");

			foreach (var guid in found)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				if (path.EndsWith($"/{settings.ScriptFilename}.cs"))
				{
					scriptPath = path;
					break;
				}
			}

			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
			var absPath = Path.Combine(projectRoot, scriptPath);

			if (File.Exists(absPath))
			{
				_outputStatusLabel.text = scriptPath;
				_outputStatusLabel.style.color = new StyleColor(new Color(0.6f, 0.9f, 0.6f));
			}
			else
			{
				_outputStatusLabel.text = "Not generated yet: " + scriptPath;
				_outputStatusLabel.style.color = new StyleColor(new Color(0.9f, 0.5f, 0.4f));
			}

			_outputPathLabel.text = scriptPath;
		}

		private void OnGenerate()
		{
			var settings = AddressableIdsEditorSettings.instance;

			if (!AddressableIdsEditorSettings.IsValidIdentifier(settings.ScriptFilename, out var idError))
			{
				Debug.LogWarning($"[ServicesExplorer] Cannot generate: {idError}");
				return;
			}

			if (!AddressableIdsEditorSettings.IsValidNamespace(settings.Namespace, out var nsError))
			{
				Debug.LogWarning($"[ServicesExplorer] Cannot generate: {nsError}");
				return;
			}

			var result = AddressableIdsGeneratorUtils.Generate(settings);

			_lastResultLabel.text = $"Last generation: {result.IdCount} ids, {result.LabelCount} labels → {result.OutputPath}";
			RefreshOutput();
		}

		private void OnRevealFile()
		{
			var settings = AddressableIdsEditorSettings.instance;
			var found = AssetDatabase.FindAssets($"t:Script {settings.ScriptFilename}");

			foreach (var guid in found)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				if (path.EndsWith($"/{settings.ScriptFilename}.cs"))
				{
					var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
					EditorUtility.RevealInFinder(Path.Combine(projectRoot, path));
					return;
				}
			}

			Debug.LogWarning($"[ServicesExplorer] {settings.ScriptFilename}.cs not found.");
		}

		private static void OnOpenAddressablesGroups()
		{
			EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
		}

		private static Label MakeInlineError()
		{
			var lbl = new Label();
			lbl.style.fontSize = 10;
			lbl.style.color = new StyleColor(new Color(1f, 0.5f, 0.4f));
			lbl.style.marginBottom = 2;
			lbl.style.display = DisplayStyle.None;
			return lbl;
		}
	}
}
