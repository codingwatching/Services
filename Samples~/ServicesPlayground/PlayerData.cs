using System;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY data class for demonstrating <see cref="DataService"/>.
	/// This is NOT part of the services package public API — it is defined inside the
	/// sample for illustration. Define your own data classes in your project.
	/// </summary>
	/// <remarks>
	/// Must be a reference type (<c>class</c>) and must have a parameterless
	/// constructor — <see cref="DataService.LoadData{T}"/> uses
	/// <see cref="System.Activator.CreateInstance{T}"/> when no saved data exists.
	/// </remarks>
	[Serializable]
	public class PlayerData
	{
		public string PlayerName = "Player";
		public int Level = 1;
		public int Coins = 0;
	}
}
