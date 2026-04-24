using System;
using UnityEditor;
using UnityEngine;

namespace GameLovers.Services.AddressableIds.Editor
{
	/// <summary>
	/// Editor-only project-level settings for the Addressable Ids Generator.
	/// Persisted to <c>ProjectSettings/AddressableIdsEditorSettings.asset</c> via <see cref="ScriptableSingleton{T}"/>.
	/// </summary>
	[FilePath("ProjectSettings/AddressableIdsEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class AddressableIdsEditorSettings : ScriptableSingleton<AddressableIdsEditorSettings>
	{
		[SerializeField] private string _scriptFilename = "AddressableId";
		[SerializeField] private string _namespace = "Game.Ids";
		[SerializeField] private string _addressableLabel = "GenerateIds";

		/// <summary>Name of the generated C# file (without extension) and the enum/class it contains.</summary>
		public string ScriptFilename
		{
			get => string.IsNullOrWhiteSpace(_scriptFilename) ? "AddressableId" : _scriptFilename;
			set
			{
				var trimmed = (value ?? "AddressableId").Trim();
				if (_scriptFilename == trimmed)
				{
					return;
				}

				_scriptFilename = trimmed;
				Save(true);
			}
		}

		/// <summary>C# namespace for the generated file.</summary>
		public string Namespace
		{
			get => string.IsNullOrWhiteSpace(_namespace) ? "Game.Ids" : _namespace;
			set
			{
				var trimmed = (value ?? "Game.Ids").Trim();
				if (_namespace == trimmed)
				{
					return;
				}

				_namespace = trimmed;
				Save(true);
			}
		}

		/// <summary>Addressables label used to filter which assets get Ids generated. Empty = generate all.</summary>
		public string AddressableLabel
		{
			get => _addressableLabel ?? "";
			set
			{
				var trimmed = (value ?? "").Trim();
				if (_addressableLabel == trimmed)
				{
					return;
				}

				_addressableLabel = trimmed;
				Save(true);
			}
		}

		/// <summary>
		/// Validates <paramref name="identifier"/> for use as a C# script filename / enum name.
		/// Returns <c>true</c> when valid; populates <paramref name="error"/> on failure.
		/// </summary>
		public static bool IsValidIdentifier(string identifier, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(identifier))
			{
				error = "Identifier cannot be empty.";
				return false;
			}

			var trimmed = identifier.Trim();

			if (char.IsDigit(trimmed[0]))
			{
				error = "Identifier must not start with a digit.";
				return false;
			}

			foreach (var c in trimmed)
			{
				if (!char.IsLetterOrDigit(c) && c != '_')
				{
					error = $"Identifier contains invalid character '{c}'. Only letters, digits, and underscores are allowed.";
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Validates <paramref name="ns"/> as a C# namespace string (dot-separated identifiers).
		/// Returns <c>true</c> when valid; populates <paramref name="error"/> on failure.
		/// </summary>
		public static bool IsValidNamespace(string ns, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(ns))
			{
				error = "Namespace cannot be empty.";
				return false;
			}

			var segments = ns.Trim().Split('.');

			foreach (var segment in segments)
			{
				if (string.IsNullOrEmpty(segment))
				{
					error = "Namespace must not contain consecutive dots or trailing dots.";
					return false;
				}

				if (!IsValidIdentifier(segment, out var segmentError))
				{
					error = $"Namespace segment \"{segment}\" is invalid: {segmentError}";
					return false;
				}
			}

			return true;
		}
	}
}
