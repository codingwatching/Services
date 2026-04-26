// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY game-state container for demonstrating <see cref="CommandService{TGameLogic}"/>.
	/// This is NOT part of the services package public API.
	/// </summary>
	/// <remarks>
	/// In a real project this would expose typed read/write APIs, sub-systems, etc.
	/// For the playground it just holds a single mutable level counter.
	/// </remarks>
	public class GameLogic
	{
		public int PlayerLevel { get; set; } = 1;
	}
}
