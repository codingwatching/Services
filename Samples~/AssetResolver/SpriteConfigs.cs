using GameLovers.Services.AssetsImporter;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.AssetResolver
{
	/// <summary>
	/// SAMPLE-ONLY <see cref="ScriptableObject"/> binding <see cref="SpriteId"/> values to
	/// <c>UnityEngine.AddressableAssets.AssetReference</c> entries pointing at <see cref="Sprite"/> assets.
	/// NOT part of the services package public API — pattern only.
	/// </summary>
	/// <remarks>
	/// Pattern: subclass <see cref="AssetConfigsScriptableObject{TId, TAsset}"/> with your id-enum and
	/// asset type. The base class records the asset type via <c>AssetType</c> (a runtime value used by
	/// <see cref="AssetResolverService"/>) and serializes a <c>List&lt;Pair&lt;TId, AssetReference&gt;&gt;</c>
	/// in <c>Configs</c>. The Addressables weak-link is intentional: the addressable tree is the source
	/// of truth at runtime; this asset is only the id-to-reference map your game code passes to
	/// <see cref="IAssetAdderService.AddConfigs{TId,TAsset}"/>.
	/// </remarks>
	[CreateAssetMenu(fileName = "SpriteConfigs", menuName = "GameLovers Services Samples/Sprite Configs")]
	public class SpriteConfigs : AssetConfigsScriptableObject<SpriteId, Sprite>
	{
	}
}
