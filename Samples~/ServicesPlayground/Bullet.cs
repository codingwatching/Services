using GameLovers.Services.Pooling;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY pooled MonoBehaviour for demonstrating <see cref="GameObjectPool{T}"/>
	/// with <see cref="IPoolEntitySpawn"/> / <see cref="IPoolEntityDespawn"/> lifecycle hooks.
	/// This is NOT part of the services package public API.
	/// </summary>
	/// <remarks>
	/// Lifecycle hooks are discovered via <c>GetComponent&lt;&gt;()</c> by
	/// <see cref="GameObjectPool{T}"/>, so they MUST be implemented on the same GameObject
	/// as the pooled component (this one).
	/// </remarks>
	public class Bullet : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn
	{
		[Tooltip("Fallback despawn timer in seconds. Off-screen detection in ServicesPlaygroundUI.Update " +
			"normally despawns bullets first; this triggers if the camera cannot be resolved.")]
		[SerializeField] private float _lifetimeSeconds = 10f;

		[Tooltip("Upward drift speed in world units / second.")]
		[SerializeField] private float _speed = 2f;

		private float _spawnTime;
		private static int _spawnCount;
		private static int _despawnCount;

		/// <summary>Total spawn-hook invocations since play started. Useful for sample UI counters.</summary>
		public static int TotalSpawns => _spawnCount;

		/// <summary>Total despawn-hook invocations since play started. Useful for sample UI counters.</summary>
		public static int TotalDespawns => _despawnCount;

		public void OnSpawn()
		{
			_spawnCount++;
			_spawnTime = Time.time;
			Debug.Log($"[Bullet] OnSpawn #{_spawnCount} at {_spawnTime:F2}s", this);
		}

		public void OnDespawn()
		{
			_despawnCount++;
			Debug.Log($"[Bullet] OnDespawn #{_despawnCount}", this);
		}

		private void Update()
		{
			// Slow upward drift in world space so the spawned ring of bullets stays inside
			// the camera frustum long enough for the user to see them.
			transform.position += Vector3.up * (_speed * Time.deltaTime);
		}

		private void OnEnable()
		{
			_spawnTime = Time.time;
		}

		private void OnDisable()
		{
		}

		/// <summary>
		/// Returns true once <see cref="_lifetimeSeconds"/> has elapsed since the last
		/// <see cref="OnSpawn"/>. Sample UI calls this to auto-despawn old bullets.
		/// </summary>
		public bool IsExpired => Time.time - _spawnTime > _lifetimeSeconds;
	}
}
