// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY message for demonstrating <see cref="MessageBrokerService"/>.
	/// This is NOT part of the services package public API.
	/// </summary>
	/// <remarks>
	/// Use a <c>struct</c> for fire-and-forget messages to avoid GC pressure.
	/// </remarks>
	public struct TestMessage : IMessage
	{
		public int Counter;
	}

	/// <summary>
	/// SAMPLE-ONLY message published by <see cref="LevelUpCommand"/> to demonstrate the
	/// command-broker round trip.
	/// </summary>
	public struct PlayerLevelledUpMessage : IMessage
	{
		public int NewLevel;
	}
}
