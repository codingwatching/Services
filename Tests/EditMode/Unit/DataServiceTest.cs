using System.Collections.Generic;
using GameLovers.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class DataServiceTest
	{
		// ReSharper disable once MemberCanBePrivate.Global
		public interface IDataMockup {}

		public class PersistentData
		{
			public string Name;
			public int Value;
		}

		private class TestableDataService : DataService
		{
			public int SaveCount;
			public string LastKey;
			public object LastInstance;
			public System.Type LastType;

			protected override void OnDataSaved(string key, object data, System.Type type)
			{
				SaveCount++;
				LastKey = key;
				LastInstance = data;
				LastType = type;
			}
		}

		private const string PersistentDataKey = nameof(PersistentData);

		private DataService _dataService;

		[SetUp]
		public void Init()
		{
			_dataService = new DataService();
			// Only delete the key(s) this fixture's tests write (DataService.SaveData/SaveAllData key on
			// typeof(T).Name) rather than PlayerPrefs.DeleteAll(), which would wipe unrelated keys shared
			// across the whole EditMode PlayerPrefs store (e.g. PerformanceTestSetup's PT_Run/PT_Settings).
			// RCR: DataService.cs LoadData<T> — a stale PersistentDataKey value left by a prior run would
			// make LoadData_NoExistingData_CreatesNew RED (loadedData.Name is the stale Name, not null).
			PlayerPrefs.DeleteKey(PersistentDataKey);
		}

		[TearDown]
		public void Cleanup()
		{
			PlayerPrefs.DeleteKey(PersistentDataKey);
		}

		[Test]
		// ADMIT: DataService.AddOrReplaceData stores the caller's instance under typeof(T) on the add path.
		// RCR: DataService.cs AddOrReplaceData — store null in the add branch → RED (GetData returns null, AreSame fails).
		// Also reddens HasData_Successfully and the save/load round trips. 2026-08-02
		public void AddData_Successfully()
		{
			var data = Substitute.For<IDataMockup>();
			
			_dataService.AddOrReplaceData(data);

			Assert.AreSame(data, _dataService.GetData<IDataMockup>());
		}

		[Test]
		public void ReplaceData_Successfully()
		{
			var data = Substitute.For<IDataMockup>();
			var data1 = new object();

			_dataService.AddOrReplaceData(data1);
			_dataService.AddOrReplaceData(data);

			Assert.AreNotSame(data1, _dataService.GetData<IDataMockup>());
			Assert.AreSame(data, _dataService.GetData<IDataMockup>());
		}

		[Test]
		// ADMIT: DataService.GetData indexes the dictionary directly, so a missing type surfaces as KeyNotFoundException
		// rather than a silent null.
		// RCR: DataService.cs GetData — swap the indexer for a TryGetValue-with-null fallback → RED (no
		// KeyNotFoundException is thrown). 2026-08-02
		public void GetData_NotFound_ThrowsException()
		{
			Assert.Throws<KeyNotFoundException>(() => _dataService.GetData<IDataMockup>());
		}

		[Test]
		// ADMIT: DataService.SaveData<T> serialises the in-memory entry to PlayerPrefs under typeof(T).Name, which is what
		// LoadData reads back.
		// RCR: DataService.cs SaveData<T> — drop the PlayerPrefs write → RED (the second service loads a fresh instance
		// with Name null). 2026-08-02
		public void SaveData_LoadData_RoundTrip_Successfully()
		{
			var data = new PersistentData { Name = "Test", Value = 123 };
			_dataService.AddOrReplaceData(data);
			_dataService.SaveData<PersistentData>();
			
			var dataService2 = new DataService();
			var loadedData = dataService2.LoadData<PersistentData>();
			
			Assert.AreEqual(data.Name, loadedData.Name);
			Assert.AreEqual(data.Value, loadedData.Value);
		}

		[Test]
		// ADMIT: DataService.LoadData<T> falls back to Activator.CreateInstance<T>() when PlayerPrefs holds no JSON for
		// the type.
		// RCR: DataService.cs LoadData<T> — always deserialize → RED (DeserializeObject on an empty string returns null,
		// Assert.IsNotNull fails). 2026-08-02
		public void LoadData_NoExistingData_CreatesNew()
		{
			var loadedData = _dataService.LoadData<PersistentData>();
			
			Assert.IsNotNull(loadedData);
			Assert.IsNull(loadedData.Name);
			Assert.AreEqual(0, loadedData.Value);
		}

		[Test]
		// ADMIT: DataService.HasData<T> reports whether the in-memory store holds an entry for the type.
		// RCR: DataService.cs HasData<T> — always return false → RED (Assert.IsTrue fails). Also reddens
		// SaveAllData_Successfully, whose second AddOrReplaceData then hits Dictionary.Add twice. 2026-08-02
		public void HasData_Successfully()
		{
			var data = new PersistentData();
			_dataService.AddOrReplaceData(data);
			
			Assert.IsTrue(_dataService.HasData<PersistentData>());
			Assert.AreSame(data, _dataService.GetData<PersistentData>());
		}

		[Test]
		// ADMIT: DataService.HasData<T> reports false for a type that was never added.
		// RCR: DataService.cs HasData<T> — always return true → RED (Assert.IsFalse fails). 2026-08-02
		public void HasData_NotFound_ReturnsFalse()
		{
			Assert.IsFalse(_dataService.HasData<PersistentData>());
		}

		[Test]
		// ADMIT: DataService.SaveAllData rewrites every in-memory entry, overwriting the earlier single-key SaveData
		// snapshot.
		// RCR: DataService.cs SaveAllData — drop the PlayerPrefs write → RED (the reload returns the stale 'Hero'
		// snapshot, not 'Alt'). 2026-08-02
		public void SaveAllData_Successfully()
		{
			var data1 = new PersistentData { Name = "Hero", Value = 10 };
			var data2 = new PersistentData { Name = "Alt", Value = 20 };

			_dataService.AddOrReplaceData(data1);
			_dataService.SaveData<PersistentData>();
			_dataService.AddOrReplaceData(data2);

			_dataService.SaveAllData();

			var dataService2 = new DataService();
			var loaded = dataService2.LoadData<PersistentData>();

			Assert.AreEqual(data2.Name, loaded.Name);
			Assert.AreEqual(data2.Value, loaded.Value);
		}

		[Test]
		// ADMIT: DataService.SaveData<T> invokes the protected OnDataSaved hook with the key, instance and type.
		// RCR: DataService.cs SaveData<T> — drop the OnDataSaved call → RED (subclass.SaveCount stays 0). 2026-08-02
		public void OnDataSaved_SubclassHook_FiresAfterSave()
		{
			var subclass = new TestableDataService();
			var data = new PersistentData { Name = "Hook", Value = 42 };
			subclass.AddOrReplaceData(data);

			subclass.SaveData<PersistentData>();

			Assert.AreEqual(1, subclass.SaveCount);
			Assert.AreEqual(typeof(PersistentData).Name, subclass.LastKey);
			Assert.AreSame(data, subclass.LastInstance);
			Assert.AreEqual(typeof(PersistentData), subclass.LastType);
		}
	}
}
