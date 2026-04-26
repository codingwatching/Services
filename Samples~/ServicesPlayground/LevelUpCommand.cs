using GameLovers.Services.Commands;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY command demonstrating <see cref="IGameCommand{TGameLogic}"/>.
	/// This is NOT part of the services package public API.
	/// </summary>
	/// <remarks>
	/// Mutates <see cref="GameLogic.PlayerLevel"/> and publishes a <see cref="PlayerLevelledUpMessage"/>
	/// so subscribers (the UI in this sample) can react.
	/// Implemented as a <c>struct</c> for fire-and-forget execution.
	/// </remarks>
	public struct LevelUpCommand : IGameCommand<GameLogic>
	{
		public void Execute(GameLogic gameLogic, IMessageBrokerService messageBroker)
		{
			gameLogic.PlayerLevel++;
			messageBroker.Publish(new PlayerLevelledUpMessage { NewLevel = gameLogic.PlayerLevel });
		}
	}
}
