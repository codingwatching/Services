using GameLovers.Services.AssetsImporter.Editor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.AssetResolver.Editor
{
	/// <summary>
	/// SAMPLE-ONLY concrete <see cref="IAssetConfigsImporter"/> for the AssetResolver
	/// sample. Reflection-discovered by <c>AssetsImporterEditorUtils.DiscoverImporters</c>
	/// so the Services Explorer "Assets Importer" tab surfaces a row from this sample.
	/// NOT part of the services package public API — define your own importer in your
	/// project (subclass <see cref="AssetsConfigsImporter{TId,TAsset,TScriptableObject}"/>).
	/// </summary>
	/// <remarks>
	/// <para>This is an empty subclass — the same shape that
	/// <c>AssetsConfigsGeneratorImporter.GenerateImporterScript</c> emits.
	/// Inherits the default <see cref="AssetsConfigsImporter{TId,TAsset,TScriptableObject}.Ids"/>
	/// implementation (all values of the <see cref="SpriteId"/> enum) and the default
	/// folder-scan <c>OnImportIds</c> from the base class.</para>
	///
	/// <para>Demo flow in the Assets Importer tab:</para>
	/// <list type="number">
	///   <item><description>Click <c>Set Path</c> on the <c>SpriteConfigsImporter</c> row → select the sample's <c>Sprites/</c> folder.</description></item>
	///   <item><description>Click <c>Import</c> → the row repopulates the sample's <c>SpriteConfigs.asset</c> by matching sprite filenames to <see cref="SpriteId"/> values.</description></item>
	/// </list>
	///
	/// <para><b>Asymmetry vs the sample's auto-setup:</b> the package's general-purpose
	/// importer (<see cref="AssetsConfigsImporterBase{TId,TAsset,TScriptableObject}.Import"/>)
	/// calls <c>Configs.Clear()</c> and re-fills from the folder scan, while the sample's
	/// <see cref="AssetResolverSampleSetup"/> post-processor preserves user mappings that
	/// already point at a non-canonical sprite. The sample README explains both flows.</para>
	/// </remarks>
	public class SpriteConfigsImporter : AssetsConfigsImporter<SpriteId, Sprite, SpriteConfigs>
	{
	}
}
