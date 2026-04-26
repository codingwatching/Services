using System;
using GameLovers.Services.Commands;
using GameLovers.Services.Pooling;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY bootstrap that constructs every foundation service, wires them through
	/// <see cref="MainInstaller"/>, and exposes them to <see cref="ServicesPlaygroundUI"/>.
	/// This is NOT part of the services package public API.
	/// </summary>
	/// <remarks>
	/// Mirrors the package <c>README.md</c> "Quick Start" section: one bind per interface,
	/// disposable services (Tick / Coroutine / Pool) are torn down via
	/// <see cref="MainInstaller.CleanDispose{T}"/> on destroy.
	/// </remarks>
	public class ServicesBootstrap : MonoBehaviour
	{
		[Tooltip("Optional prefab for the GameObjectPool<Bullet> demo. If left empty, a sphere primitive is generated at runtime so the sample is true zero-setup.")]
		[SerializeField] private Bullet _bulletPrefab;

		[Tooltip("Initial seed for the deterministic RngService.")]
		[SerializeField] private int _rngSeed = 42;

		[Tooltip("Initial pool size for the bullet GameObjectPool<Bullet>.")]
		[SerializeField] private uint _bulletPoolInitSize = 8;

		public IMessageBrokerService MessageBroker { get; private set; }
		public ITickService Tick { get; private set; }
		public ICoroutineService Coroutine { get; private set; }
		public IPoolService Pool { get; private set; }
		public IDataService Data { get; private set; }
		public ITimeManipulator Time { get; private set; }
		public IRngService Rng { get; private set; }
		public ICommandService<GameLogic> Commands { get; private set; }
		public GameLogic GameLogic { get; private set; }
		public Bullet BulletPrefab => _bulletPrefab;

		private void Awake()
		{
			MessageBroker = new MessageBrokerService();
			Tick          = new TickService();
			Coroutine     = new CoroutineService();
			Data          = new DataService();
			Time          = new TimeService();
			Rng           = new RngService(RngService.CreateRngData(_rngSeed));
			Pool          = new PoolService();
			GameLogic     = new GameLogic();
			Commands      = new CommandService<GameLogic>(GameLogic, MessageBroker);

			MainInstaller.Bind<IMessageBrokerService>(MessageBroker);
			MainInstaller.Bind<ITickService>(Tick);
			MainInstaller.Bind<ICoroutineService>(Coroutine);
			MainInstaller.Bind<IDataService>(Data);
			MainInstaller.Bind<ITimeManipulator>(Time);
			MainInstaller.Bind<IRngService>(Rng);
			MainInstaller.Bind<IPoolService>(Pool);
			MainInstaller.Bind<ICommandService<GameLogic>>(Commands);

			Pool.AddPool(new GameObjectPool<Bullet>(_bulletPoolInitSize, GetOrCreateBulletPrefab()));
		}

		private Bullet GetOrCreateBulletPrefab()
		{
			if (_bulletPrefab != null)
			{
				return _bulletPrefab;
			}

			// Generate a sphere primitive on the fly so the sample needs no prefab asset.
			// We disable + DontDestroyOnLoad it so the pool's Instantiator clones a hidden,
			// scene-stable sample entity (matches the contract of GameObjectPool<T>).
			var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			go.name = "BulletSample";
			go.transform.localScale = Vector3.one * 0.4f;

			var collider = go.GetComponent<Collider>();
			if (collider != null)
			{
				Destroy(collider);
			}

			ApplyBulletMaterialColor(go);

			_bulletPrefab = go.AddComponent<Bullet>();
			go.SetActive(false);
			DontDestroyOnLoad(go);
			return _bulletPrefab;
		}

		/// <summary>
		/// Tints the runtime-generated bullet prefab a bright orange and turns on emission
		/// so the sphere remains visible without any scene lighting setup. The Lit shader
		/// (URP / HDRP / Built-in Standard) renders close to black under a fresh empty
		/// scene, which made the pool demo look broken even though the spawns were working.
		///
		/// We set:
		/// - <c>_BaseColor</c> (URP / HDRP Lit) and <c>_Color</c> (Built-in Standard) for the diffuse tint.
		/// - <c>_EmissionColor</c> + <c>_EMISSION</c> keyword so the sphere self-illuminates regardless of lights.
		/// - <c>globalIlluminationFlags = None</c> so the editor doesn't try to bake the runtime color.
		/// </summary>
		private static void ApplyBulletMaterialColor(GameObject go)
		{
			var renderer = go.GetComponent<MeshRenderer>();
			if (renderer == null)
			{
				return;
			}

			var mat = renderer.material; // forces a unique instance
			if (mat == null)
			{
				return;
			}

			var color = new Color(1f, 0.55f, 0.1f, 1f);
			if (mat.HasProperty("_BaseColor"))
			{
				mat.SetColor("_BaseColor", color);
			}
			if (mat.HasProperty("_Color"))
			{
				mat.SetColor("_Color", color);
			}

			// Emissive intensity 1.6× the base color reads cleanly against an unlit / dark
			// editor scene without blowing out HDR. Both URP Lit and Built-in Standard
			// expose _EmissionColor; the _EMISSION keyword must be enabled for Built-in.
			var emission = color * 1.6f;
			emission.a = 1f;

			if (mat.HasProperty("_EmissionColor"))
			{
				mat.SetColor("_EmissionColor", emission);
				mat.EnableKeyword("_EMISSION");
				mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
			}
		}

		private async void Start()
		{
			// Loads version-data.txt from Resources. The package's editor utility
			// (Editor/Versioning/VersionEditorUtils.cs) writes this file on every domain
			// reload. If you imported this sample into a project that has not yet had a
			// domain reload, the load will fail and Versioning fields stay empty.
			try
			{
				await VersionServices.LoadVersionDataAsync();
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[ServicesBootstrap] Version data load failed: {e.Message}", this);
			}
		}

		private void OnDestroy()
		{
			// Disposable services first (they own DontDestroyOnLoad host GameObjects /
			// pooled instances). Order is intentional — Pool may indirectly hold Bullet
			// instances that depend on Tick callbacks during disposal.
			TryCleanDispose<IPoolService>();
			TryCleanDispose<ICoroutineService>();
			TryCleanDispose<ITickService>();

			MainInstaller.Clean();
		}

		private static void TryCleanDispose<T>() where T : class, IDisposable
		{
			if (MainInstaller.TryResolve<T>(out _))
			{
				MainInstaller.CleanDispose<T>();
			}
		}
	}
}
