// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.AssetResolver
{
	/// <summary>
	/// SAMPLE-ONLY enum used as the typed identifier (TId) for the
	/// <see cref="SpriteConfigs"/> asset registry. NOT part of the services
	/// package public API — define your own enum in your project.
	/// </summary>
	/// <remarks>
	/// In a real project you would generate this enum via
	/// <c>Tools &gt; GameLovers &gt; Addressable Ids &gt; Generate Addressable Ids</c>
	/// from the labels you applied to your Addressables. The sample defines it by hand
	/// so the sample is self-contained and the user does not need to run the generator
	/// before pressing Play.
	/// </remarks>
	public enum SpriteId
	{
		Hero = 0,
		Coin = 1,
		Enemy = 2
	}
}
