using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameLovers.Services.Commands;
using GameLovers.Services.Pooling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace

namespace GameLovers.Services.Samples.ServicesPlayground
{
	/// <summary>
	/// SAMPLE-ONLY uGUI driver for the Services Playground. The Canvas hierarchy lives in
	/// <c>ServicesPlaygroundUI.prefab</c>; this script holds <c>[SerializeField]</c> references
	/// to every Button + the log / live-status Texts and wires <c>onClick.AddListener</c>
	/// in <see cref="Awake"/>. The driver is on the prefab's Canvas root; it discovers the
	/// scene's <see cref="ServicesBootstrap"/> via <see cref="FindAnyObjectByType{T}"/> at startup.
	/// </summary>
	public class ServicesPlaygroundUI : MonoBehaviour
	{
		private const int LogLineCap = 30;

		[Header("Tuning")]
		[Tooltip("Override Time.AddTime delta in seconds when 'Add time' is invoked.")]
		[SerializeField] private float _timeAddDeltaSeconds = 60f;

		[Tooltip("Number of bullets to spawn per 'Spawn burst' invocation.")]
		[SerializeField] private int _spawnBurstCount = 5;

		[Header("Status panes")]
		[SerializeField] private TMP_Text _log;
		[SerializeField] private TMP_Text _liveStatus;
		[SerializeField] private ScrollRect _logScrollRect;

		[Header("Installer buttons")]
		[SerializeField] private Button _dumpBindingsButton;
		[SerializeField] private Button _cleanAllButton;

		[Header("Messaging buttons")]
		[SerializeField] private Button _subscribeButton;
		[SerializeField] private Button _publishButton;
		[SerializeField] private Button _publishSafeButton;
		[SerializeField] private Button _unsubscribeButton;

		[Header("Tick buttons")]
		[SerializeField] private Button _subUpdateButton;
		[SerializeField] private Button _subFixedUpdateButton;
		[SerializeField] private Button _subLateUpdateButton;
		[SerializeField] private Button _tickUnsubscribeAllButton;

		[Header("Coroutine buttons")]
		[SerializeField] private Button _delayCallButton;
		[SerializeField] private Button _asyncCoroutineButton;
		[SerializeField] private Button _stopAllCoroutinesButton;

		[Header("Pool buttons")]
		[SerializeField] private Button _spawnBurstButton;
		[SerializeField] private Button _despawnAllButton;

		[Header("Data buttons")]
		[SerializeField] private Button _loadDataButton;
		[SerializeField] private Button _modifyAndSaveButton;
		[SerializeField] private Button _saveAllButton;
		[SerializeField] private Button _deletePrefsButton;

		[Header("Time buttons")]
		[SerializeField] private Button _addTimeButton;
		[SerializeField] private Button _resetTimeButton;

		[Header("RNG buttons")]
		[SerializeField] private Button _drawNextButton;
		[SerializeField] private Button _peekNextButton;
		[SerializeField] private Button _restoreToZeroButton;

		[Header("Commands buttons")]
		[SerializeField] private Button _levelUpButton;

		[Header("Versioning buttons")]
		[SerializeField] private Button _dumpVersionButton;

		private ServicesBootstrap _bootstrap;
		private readonly StringBuilder _logBuffer = new StringBuilder();
		private int _logLineCount;
		private readonly List<Bullet> _bulletDespawnBuffer = new List<Bullet>();
		private Camera _mainCamera;

		private int _testMessagesReceived;
		private int _updateTicks;
		private int _fixedTicks;
		private int _lateTicks;

		private Action<TestMessage>             _onTestMessage;
		private Action<PlayerLevelledUpMessage> _onLevelledUp;
		private Action<float>                   _onUpdateTick;
		private Action<float>                   _onFixedTick;
		private Action<float>                   _onLateTick;

		private void Awake()
		{
			_bootstrap     = FindAnyObjectByType<ServicesBootstrap>();
			_onTestMessage = OnTestMessage;
			_onLevelledUp  = OnLevelledUp;
			_onUpdateTick  = OnUpdateTick;
			_onFixedTick   = OnFixedTick;
			_onLateTick    = OnLateTick;

			EnsureInputModuleOnEventSystem();

			WireButton(_dumpBindingsButton,        Installer_DumpBindings);
			WireButton(_cleanAllButton,            Installer_CleanAll);

			WireButton(_subscribeButton,           Messaging_Subscribe);
			WireButton(_publishButton,             Messaging_Publish);
			WireButton(_publishSafeButton,         Messaging_PublishSafe);
			WireButton(_unsubscribeButton,         Messaging_Unsubscribe);

			WireButton(_subUpdateButton,           Tick_SubscribeUpdate);
			WireButton(_subFixedUpdateButton,      Tick_SubscribeFixed);
			WireButton(_subLateUpdateButton,       Tick_SubscribeLate);
			WireButton(_tickUnsubscribeAllButton,  Tick_UnsubscribeAll);

			WireButton(_delayCallButton,           Coroutine_StartDelay);
			WireButton(_asyncCoroutineButton,      Coroutine_StartAsync);
			WireButton(_stopAllCoroutinesButton,   Coroutine_StopAll);

			WireButton(_spawnBurstButton,          Pool_SpawnBurst);
			WireButton(_despawnAllButton,          Pool_DespawnAll);

			WireButton(_loadDataButton,            Data_Load);
			WireButton(_modifyAndSaveButton,       Data_ModifyAndSave);
			WireButton(_saveAllButton,             Data_SaveAll);
			WireButton(_deletePrefsButton,         Data_DeletePrefs);

			WireButton(_addTimeButton,             Time_AddTime);
			WireButton(_resetTimeButton,           Time_Reset);

			WireButton(_drawNextButton,            Rng_DrawNext);
			WireButton(_peekNextButton,            Rng_PeekNext);
			WireButton(_restoreToZeroButton,       Rng_RestoreToZero);

			WireButton(_levelUpButton,             Commands_LevelUp);

			WireButton(_dumpVersionButton,         Versioning_DumpVersion);
		}

		private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
		{
			if (button != null)
			{
				button.onClick.AddListener(action);
			}
		}

		/// <summary>
		/// Ensures the scene's <see cref="EventSystem"/> has an input module compatible with the
		/// project's Active Input Handling setting. Editor-time scene generation defaults to
		/// <see cref="StandaloneInputModule"/> (legacy); this swaps to
		/// <c>InputSystemUIInputModule</c> when the New Input System is the active package
		/// (<c>ENABLE_INPUT_SYSTEM</c> is defined). Without this swap, the legacy module would
		/// throw <c>InvalidOperationException</c> on <c>UnityEngine.Input.mousePosition</c>
		/// every frame under New-Input-only.
		/// </summary>
		private static void EnsureInputModuleOnEventSystem()
		{
			var es = FindAnyObjectByType<EventSystem>();
			if (es == null)
			{
				return;
			}
			var go = es.gameObject;
#if ENABLE_INPUT_SYSTEM
			if (go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null)
			{
				return;
			}
			var legacy = go.GetComponent<StandaloneInputModule>();
			if (legacy != null)
			{
				DestroyImmediate(legacy);
			}
			go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
			if (go.GetComponent<StandaloneInputModule>() == null)
			{
				go.AddComponent<StandaloneInputModule>();
			}
#endif
		}

		private void Start()
		{
			if (_bootstrap == null)
			{
				Append("ServicesBootstrap not found in scene. Add a Bootstrap GameObject with ServicesBootstrap.");
				return;
			}

			_bootstrap.MessageBroker.Subscribe(_onLevelledUp);
			Append("Bootstrap ready. Resolve services via MainInstaller or _bootstrap.*");
		}

		private void Update()
		{
			if (_bootstrap == null)
			{
				return;
			}

			if (_liveStatus != null)
			{
				var time = _bootstrap.Time;
				_liveStatus.text =
					$"UTC: {time.DateTimeUtcNow:HH:mm:ss}\n" +
					$"Unity: {time.UnityTimeNow:F1}s  Scaled: {time.UnityScaleTimeNow:F1}s\n" +
					$"Messages received: {_testMessagesReceived}\n" +
					$"Ticks U/F/L: {_updateTicks} / {_fixedTicks} / {_lateTicks}\n" +
					$"Bullet OnSpawn / OnDespawn lifetime calls: {Bullet.TotalSpawns} / {Bullet.TotalDespawns}\n" +
					$"RNG counter: {_bootstrap.Rng.Counter}\n" +
					$"Player level (GameLogic): {_bootstrap.GameLogic.PlayerLevel}";
			}

			TickPoolExpiration();
		}

		/// <summary>
		/// Returns spawned bullets to the pool when they have drifted off the visible camera
		/// frustum (or when their <see cref="Bullet.IsExpired"/> fallback timer fires). Iterates
		/// a snapshot to avoid <c>InvalidOperationException</c> from modifying the pool's
		/// spawned list during enumeration.
		/// </summary>
		private void TickPoolExpiration()
		{
			if (!_bootstrap.Pool.TryGetPool<Bullet>(out var pool))
			{
				return;
			}

			if (_mainCamera == null)
			{
				_mainCamera = Camera.main;
			}

			_bulletDespawnBuffer.Clear();
			foreach (var bullet in pool.SpawnedReadOnly)
			{
				if (bullet == null)
				{
					continue;
				}

				if (_mainCamera != null)
				{
					var view = _mainCamera.WorldToViewportPoint(bullet.transform.position);
					var offScreen = view.z < 0f
						|| view.x < -0.05f || view.x > 1.05f
						|| view.y < -0.05f || view.y > 1.05f;
					if (offScreen)
					{
						_bulletDespawnBuffer.Add(bullet);
						continue;
					}
				}

				if (bullet.IsExpired)
				{
					_bulletDespawnBuffer.Add(bullet);
				}
			}

			for (var i = 0; i < _bulletDespawnBuffer.Count; i++)
			{
				pool.Despawn(_bulletDespawnBuffer[i]);
			}
			_bulletDespawnBuffer.Clear();
		}

		private void OnDestroy()
		{
			if (_bootstrap?.MessageBroker != null)
			{
				_bootstrap.MessageBroker.UnsubscribeAll(this);
			}
			if (_bootstrap?.Tick != null)
			{
				_bootstrap.Tick.UnsubscribeAll(this);
			}
		}

		// ---------------- Installer ----------------

		public void Installer_DumpBindings()
		{
			Append("Installer: see Services Explorer > Installer tab for the live bindings table.");
		}

		public void Installer_CleanAll()
		{
			MainInstaller.Clean();
			Append("Installer: MainInstaller.Clean() — all bindings removed. Most other actions will now NullRef.");
		}

		// ---------------- Message Broker ----------------

		public void Messaging_Subscribe()
		{
			_bootstrap.MessageBroker.Subscribe(_onTestMessage);
			Append("Messaging: Subscribed handler for TestMessage.");
		}

		public void Messaging_Publish()
		{
			_bootstrap.MessageBroker.Publish(new TestMessage { Counter = _testMessagesReceived + 1 });
			Append("Messaging: Publish<TestMessage>() — see counter in live status.");
		}

		public void Messaging_PublishSafe()
		{
			_bootstrap.MessageBroker.PublishSafe(new TestMessage { Counter = _testMessagesReceived + 1 });
			Append("Messaging: PublishSafe<TestMessage>() — safe during chained subscribe/unsubscribe.");
		}

		public void Messaging_Unsubscribe()
		{
			_bootstrap.MessageBroker.Unsubscribe<TestMessage>(this);
			Append("Messaging: Unsubscribe<TestMessage>(this).");
		}

		// ---------------- Tick ----------------

		public void Tick_SubscribeUpdate()
		{
			_bootstrap.Tick.SubscribeOnUpdate(_onUpdateTick);
			Append("Tick: SubscribeOnUpdate.");
		}

		public void Tick_SubscribeFixed()
		{
			_bootstrap.Tick.SubscribeOnFixedUpdate(_onFixedTick);
			Append("Tick: SubscribeOnFixedUpdate.");
		}

		public void Tick_SubscribeLate()
		{
			_bootstrap.Tick.SubscribeOnLateUpdate(_onLateTick);
			Append("Tick: SubscribeOnLateUpdate.");
		}

		public void Tick_UnsubscribeAll()
		{
			_bootstrap.Tick.UnsubscribeAll();
			Append("Tick: UnsubscribeAll() — all lists cleared.");
		}

		// ---------------- Coroutine ----------------

		public void Coroutine_StartDelay()
		{
			var handle = _bootstrap.Coroutine.StartDelayCall(() =>
			{
				Append("Coroutine: delayed call fired (2s).");
			}, 2f);

			Append($"Coroutine: StartDelayCall(2s) running={handle.IsRunning}.");
		}

		public void Coroutine_StartAsync()
		{
			// 3-second wait so the handle is visibly present in Services Explorer >
			// Coroutine tab across multiple refresh cycles (refresh interval is 250ms).
			// A short WaitFrames(60) used to complete inside a single refresh cycle on
			// editor framerates above ~60 fps and looked like the button was a no-op.
			var handle = _bootstrap.Coroutine.StartAsyncCoroutine(WaitSeconds(3f));
			handle.OnComplete(() =>
			{
				Append("Coroutine: 3s async coroutine completed.");
			});
			Append("Coroutine: StartAsyncCoroutine(WaitSeconds 3) started — visible in Services Explorer > Coroutine tab.");
		}

		public void Coroutine_StopAll()
		{
			_bootstrap.Coroutine.StopAllCoroutines();
			Append("Coroutine: StopAllCoroutines() — running coroutines aborted.");
		}

		private static IEnumerator WaitFrames(int frames)
		{
			for (var i = 0; i < frames; i++)
			{
				yield return null;
			}
		}

		private static IEnumerator WaitSeconds(float seconds)
		{
			yield return new WaitForSeconds(seconds);
		}

		// ---------------- Pool ----------------

		public void Pool_SpawnBurst()
		{
			if (!_bootstrap.Pool.TryGetPool<Bullet>(out var pool))
			{
				Append("Pool: no Bullet pool registered.");
				return;
			}

			// Spawn a ring of bullets near the bottom of the camera frustum so they have room
			// to drift upward into view. The camera at (0,1,-10) with FOV 60° sees roughly
			// y in [-4.8, 6.8] at z=0; centering at y=-2.5 puts the ring in the lower third
			// of the screen, with a 1.5-unit radius producing a clear pentagon-of-bullets.
			const float Radius = 1.5f;
			const float SpawnCenterY = -2.5f;
			for (var i = 0; i < _spawnBurstCount; i++)
			{
				var bullet = pool.Spawn();
				var angleRad = i * (Mathf.PI * 2f / _spawnBurstCount);
				var pos = new Vector3(Mathf.Cos(angleRad) * Radius, SpawnCenterY + Mathf.Sin(angleRad) * Radius, 0f);
				bullet.transform.SetPositionAndRotation(pos, Quaternion.identity);
			}
			Append($"Pool: Spawn x{_spawnBurstCount} (ring near bottom of screen).");
		}

		public void Pool_DespawnAll()
		{
			_bootstrap.Pool.DespawnAll<Bullet>();
			Append("Pool: DespawnAll<Bullet>().");
		}

		// ---------------- Data ----------------

		public void Data_Load()
		{
			var data = _bootstrap.Data.LoadData<PlayerData>();
			Append($"Data: LoadData<PlayerData>() = {data.PlayerName}, lvl {data.Level}, coins {data.Coins}.");
		}

		public void Data_ModifyAndSave()
		{
			if (!_bootstrap.Data.HasData<PlayerData>())
			{
				_bootstrap.Data.LoadData<PlayerData>();
			}
			var data = _bootstrap.Data.GetData<PlayerData>();
			data.Level++;
			data.Coins += 10;
			_bootstrap.Data.SaveData<PlayerData>();
			Append($"Data: SaveData<PlayerData>() — lvl {data.Level}, coins {data.Coins}.");
		}

		public void Data_SaveAll()
		{
			_bootstrap.Data.SaveAllData();
			Append("Data: SaveAllData() — every loaded type flushed to PlayerPrefs.");
		}

		public void Data_DeletePrefs()
		{
			PlayerPrefs.DeleteKey(typeof(PlayerData).Name);
			PlayerPrefs.Save();
			Append("Data: PlayerPrefs key removed (next LoadData<PlayerData> will create a fresh instance).");
		}

		// ---------------- Time ----------------

		public void Time_AddTime()
		{
			_bootstrap.Time.AddTime(_timeAddDeltaSeconds);
			Append($"Time: AddTime({_timeAddDeltaSeconds}s) — DateTimeUtcNow advances accordingly.");
		}

		public void Time_Reset()
		{
			_bootstrap.Time.AddTime(-_bootstrap.Time.UnityTimeNow + UnityEngine.Time.realtimeSinceStartup);
			_bootstrap.Time.SetInitialTime(DateTime.Now);
			Append("Time: SetInitialTime(DateTime.Now) — clock re-anchored to wall time.");
		}

		// ---------------- RNG ----------------

		public void Rng_DrawNext()
		{
			var roll = _bootstrap.Rng.Range(1, 7);
			Append($"RNG: Range(1, 7) = {roll}, counter now {_bootstrap.Rng.Counter}.");
		}

		public void Rng_PeekNext()
		{
			var peek = _bootstrap.Rng.PeekRange(1, 7);
			Append($"RNG: PeekRange(1, 7) = {peek} (counter UNCHANGED at {_bootstrap.Rng.Counter}).");
		}

		public void Rng_RestoreToZero()
		{
			_bootstrap.Rng.Restore(0);
			Append("RNG: Restore(0) — replay sequence from the start.");
		}

		// ---------------- Commands ----------------

		public void Commands_LevelUp()
		{
			_bootstrap.Commands.ExecuteCommand(new LevelUpCommand());
			Append("Commands: ExecuteCommand(LevelUpCommand) — broker should publish PlayerLevelledUpMessage.");
		}

		// ---------------- Versioning ----------------

		public void Versioning_DumpVersion()
		{
			Append($"Versioning: VersionExternal = {VersionServices.VersionExternal} (always safe).");
			try
			{
				Append($"Versioning: VersionInternal = {VersionServices.VersionInternal}");
				Append($"Versioning: Branch = '{VersionServices.Branch}'  Commit = '{VersionServices.Commit}'  Build = '{VersionServices.BuildNumber}'");
			}
			catch (Exception e)
			{
				Append($"Versioning: load not complete — {e.Message}");
			}
		}

		// ---------------- Subscription handlers ----------------

		private void OnTestMessage(TestMessage msg)
		{
			_testMessagesReceived++;
		}

		private void OnLevelledUp(PlayerLevelledUpMessage msg)
		{
			Append($"Broker: PlayerLevelledUpMessage received (level {msg.NewLevel}).");
		}

		private void OnUpdateTick(float dt)  { _updateTicks++; }
		private void OnFixedTick(float dt)   { _fixedTicks++; }
		private void OnLateTick(float dt)    { _lateTicks++; }

		// ---------------- Logging ----------------

		private void Append(string line)
		{
			Debug.Log("[ServicesPlayground] " + line);
			if (_log == null)
			{
				return;
			}

			// Snapshot whether the user is currently parked at the bottom of the log. If they
			// have dragged the scroll view up to read history, we preserve their position; if
			// they are at the bottom, we keep them pinned to the latest line on each append.
			var wasAtBottom = _logScrollRect == null || _logScrollRect.verticalNormalizedPosition < 0.05f;

			if (_logLineCount >= LogLineCap)
			{
				var firstNewline = _logBuffer.ToString().IndexOf('\n');
				if (firstNewline >= 0)
				{
					_logBuffer.Remove(0, firstNewline + 1);
					_logLineCount--;
				}
			}

			_logBuffer.Append(line);
			_logBuffer.Append('\n');
			_logLineCount++;
			_log.text = _logBuffer.ToString();

			if (_logScrollRect != null && wasAtBottom)
			{
				// Layout must rebuild before we can move the viewport — otherwise
				// verticalNormalizedPosition reads against stale content size.
				Canvas.ForceUpdateCanvases();
				_logScrollRect.verticalNormalizedPosition = 0f;
			}
		}
	}
}
