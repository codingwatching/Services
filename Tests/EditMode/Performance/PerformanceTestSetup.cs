using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.PerformanceTesting.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	/// <summary>
	/// Prebuild setup for performance tests.
	/// This ensures the Unity Performance Testing Package has the required metadata
	/// before tests run in EditMode.
	/// </summary>
	public class PerformanceTestSetup : IPrebuildSetup
	{
		// Mirrors Unity.PerformanceTesting.Runtime.Utils.PlayerPrefKeyRunJSON / PlayerPrefKeySettingsJSON.
		// Both keys are reproduced here because Utils is `internal` to com.unity.test-framework.performance.
		private const string PlayerPrefKeyRunJSON = "PT_Run";
		private const string PlayerPrefKeySettingsJSON = "PT_Settings";

		// Default RunSettings JSON. MeasurementCount = -1 is the package's "no override" sentinel —
		// MethodMeasurement.SettingsOverride() early-returns when count < 0, preserving the per-test
		// .WarmupCount(...) / .MeasurementCount(...) configuration.
		private const string DefaultRunSettingsJson = "{\"MeasurementCount\":-1}";

		public void Setup()
		{
			InitializePerformanceTestMetadata();
		}

		/// <summary>
		/// Initializes performance test metadata. Call this from [OneTimeSetUp] in test fixtures
		/// to ensure metadata is available before tests run.
		/// </summary>
		/// <remarks>
		/// Two PlayerPrefs entries are required to make `Measure.Method(...).Run()` succeed in EditMode:
		///   - PT_Run      — full Run metadata (editor info, dependencies, build settings); consumed by
		///                   Metadata.SetRuntimeSettings() when results are emitted.
		///   - PT_Settings — RunSettings (measurement-count override); consumed by
		///                   MethodMeasurement.SettingsOverride() *before* the first warmup runs.
		/// Omitting PT_Settings causes RunSettings.Instance to lazy-load from an empty JSON string,
		/// JsonUtility throws, ResourcesLoader swallows the exception and returns null, and
		/// SettingsOverride() then NREs on `RunSettings.Instance.MeasurementCount`.
		/// </remarks>
		public static void InitializePerformanceTestMetadata()
		{
			var run = CreateRunInfo();
			SaveToPrefs(run, PlayerPrefKeyRunJSON);

			PlayerPrefs.SetString(PlayerPrefKeySettingsJSON, DefaultRunSettingsJson);

			PlayerPrefs.Save();

			Debug.Log("[PerformanceTestSetup] Performance test metadata initialized.");
		}

		private static Run CreateRunInfo()
		{
			var run = new Run
			{
				Editor = GetEditorInfo(),
				Dependencies = GetPackageDependencies(),
				Date = ConvertToUnixTimestamp(DateTime.Now),
				Player = new Player()
			};

			SetBuildSettings(run);
			return run;
		}

		private static Unity.PerformanceTesting.Data.Editor GetEditorInfo()
		{
			var fullVersion = UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion();
			const string pattern = @"(.+\.+.+\.\w+)|((?<=\().+(?=\)))";
			var matches = Regex.Matches(fullVersion, pattern);

			return new Unity.PerformanceTesting.Data.Editor
			{
				Branch = GetEditorBranch(),
				Version = matches.Count > 0 ? matches[0].Value : "unknown",
				Changeset = matches.Count > 1 ? matches[1].Value : "unknown",
				Date = UnityEditorInternal.InternalEditorUtility.GetUnityVersionDate(),
			};
		}

		private static string GetEditorBranch()
		{
			foreach (var method in typeof(UnityEditorInternal.InternalEditorUtility).GetMethods())
			{
				if (method.Name.Contains("GetUnityBuildBranch"))
				{
					return (string)method.Invoke(null, null);
				}
			}
			return "null";
		}

		private static List<string> GetPackageDependencies()
		{
			var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			return packages.Select(p => $"{p.name}@{p.version}").ToList();
		}

		private static void SetBuildSettings(Run run)
		{
			run.Player.GpuSkinning = PlayerSettings.gpuSkinning;
			run.Player.ScriptingBackend = PlayerSettings
				.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup))
				.ToString();
			run.Player.RenderThreadingMode = PlayerSettings.graphicsJobs
				? PlayerSettings.graphicsJobMode.ToString()
				: PlayerSettings.MTRendering ? "MultiThreaded" : "SingleThreaded";
			run.Player.AndroidTargetSdkVersion = PlayerSettings.Android.targetSdkVersion.ToString();
			run.Player.AndroidBuildSystem = EditorUserBuildSettings.androidBuildSystem.ToString();
			run.Player.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
			run.Player.StereoRenderingPath = PlayerSettings.stereoRenderingPath.ToString();
		}

		private static long ConvertToUnixTimestamp(DateTime date)
		{
			var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			var diff = date.ToUniversalTime() - origin;
			return (long)Math.Floor(diff.TotalSeconds);
		}

		private static void SaveToPrefs(object obj, string key)
		{
			var json = JsonUtility.ToJson(obj, true);
			PlayerPrefs.SetString(key, json);
		}
	}
}
