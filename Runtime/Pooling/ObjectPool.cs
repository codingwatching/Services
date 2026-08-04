using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace GameLovers.Services.Pooling
{
	/// <inheritdoc />
	public abstract class ObjectPoolBase<T> : IObjectPool<T> where T : class
	{
		protected readonly IList<T> SpawnedEntities = new List<T>();

		private readonly Stack<T> _stack;
		private readonly Func<T, T> _instantiator;
		
		private T _sampleEntity;
		
		/// <inheritdoc />
		public T SampleEntity => _sampleEntity;

		/// <inheritdoc />
		public IReadOnlyList<T> SpawnedReadOnly => SpawnedEntities as IReadOnlyList<T>;

		protected ObjectPoolBase(uint initSize, T sampleEntity, Func<T, T> instantiator)
		{
			_sampleEntity = sampleEntity;
			_instantiator = instantiator;
			_stack = new Stack<T>((int)initSize);

			for (var i = 0; i < initSize; i++)
			{
				_stack.Push(CallInstantiator());
			}
		}

		/// <inheritdoc />
		public bool IsSpawned(Func<T, bool> conditionCheck)
		{
			for (var i = 0; i < SpawnedEntities.Count; i++)
			{
				if (conditionCheck(SpawnedEntities[i]))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc />
		public void Reset(uint initSize, T sampleEntity)
		{
			Dispose();
			
			_sampleEntity = sampleEntity;

			for (var i = 0; i < initSize; i++)
			{
				_stack.Push(CallInstantiator());
			}
		}

		/// <inheritdoc />
		public List<T> Clear()
		{
			var ret = new List<T>(SpawnedEntities);

			ret.AddRange(_stack);
			SpawnedEntities.Clear();
			_stack.Clear();

			return ret;
		}

		/// <inheritdoc />
		public void DespawnAll()
		{
			for (var i = SpawnedEntities.Count - 1; i > -1; i--)
			{
				Despawn(SpawnedEntities[i]);
			}
		}

		/// <summary>
		/// Clears the pool, additionally dropping the reference to <see cref="IObjectPool{T}.SampleEntity"/>
		/// when <paramref name="disposeSampleEntity"/> is set.
		/// </summary>
		public virtual void Dispose(bool disposeSampleEntity)
		{
			if (disposeSampleEntity)
			{
				_sampleEntity = null;
			}
			
			Dispose();
		}

		/// <inheritdoc />
		public T Spawn()
		{
			var entity = SpawnEntity();

			CallOnSpawned(entity);

			return entity;
		}

		/// <inheritdoc />
		public T Spawn<TData>(TData data)
		{
			var entity = SpawnEntity();

			CallOnSpawned(entity);
			CallOnSpawned(entity, data);

			return entity;
		}

		/// <inheritdoc />
		public bool Despawn(T entity)
		{
			if (!SpawnedEntities.Remove(entity) || entity == null || entity.Equals(null))
			{
				return false;
			}

			_stack.Push(entity);
			CallOnDespawned(entity);
			PostDespawnEntity(entity);

			return true;
		}

		/// <inheritdoc />
		public bool Despawn(bool onlyFirst, Func<T, bool> entityGetter)
		{
			var despawned = false;

			for (var i = 0; i < SpawnedEntities.Count; i++)
			{
				if (!entityGetter(SpawnedEntities[i]))
				{
					continue;
				}

				// Despawn(entity) removes the first occurrence from SpawnedEntities, shifting
				// subsequent items down by one. Step back so the next iteration revisits the
				// current index, otherwise adjacent matches would be skipped.
				if (Despawn(SpawnedEntities[i]))
				{
					despawned = true;
					i--;
				}

				if (onlyFirst)
				{
					break;
				}
			}

			return despawned;
		}

		/// <inheritdoc />
		public virtual void Dispose()
		{
			Clear();
		}

		/// <summary>
		/// Takes the next entity from the stack, instantiating one when the stack is empty.
		/// Retries past entities an external owner destroyed while they sat pooled, which is why the
		/// null test goes through <c>IsDestroyedOrNull</c> rather than a plain <c>== null</c>.
		/// </summary>
		protected virtual T SpawnEntity()
		{
			T entity = null;

			do
			{
				entity = _stack.Count == 0 ? CallInstantiator() : _stack.Pop();
			}
			// Need to do while loop and check as parent objects could have destroyed the entity/gameobject before it could
			// be properly disposed by pool service
			while (IsDestroyedOrNull(entity));

			SpawnedEntities.Add(entity);

			return entity;
		}

		/// <summary>
		/// Runs after an entity has been returned to the stack; the base implementation does nothing.
		/// </summary>
		protected virtual void PostDespawnEntity(T entity) { }

		/// <summary>
		/// Instantiates a fresh entity from the sample and, when it implements
		/// <see cref="IPoolEntityObject{T}"/>, hands it back a reference to this pool.
		/// </summary>
		protected T CallInstantiator()
		{
			var entity = _instantiator.Invoke(SampleEntity);
			var poolEntity = entity as IPoolEntityObject<T>;

			poolEntity?.Init(this);

			return entity;
		}

		/// <summary>
		/// Dispatches the spawn hook. Override to change how a pooled entity is discovered —
		/// this base casts the entity directly, whereas the GameObject pools use <c>GetComponent</c>.
		/// </summary>
		protected virtual void CallOnSpawned(T entity)
		{
			var poolEntity = entity as IPoolEntitySpawn;

			poolEntity?.OnSpawn();
		}

		/// <summary>
		/// Dispatches the data-carrying spawn hook; see the parameterless overload for the cast rationale.
		/// </summary>
		protected virtual void CallOnSpawned<TData>(T entity, TData data)
		{
			var poolEntity = entity as IPoolEntitySpawn<TData>;

			poolEntity?.OnSpawn(data);
		}

		/// <summary>
		/// Dispatches the despawn hook; see <see cref="CallOnSpawned(T)"/> for the cast rationale.
		/// </summary>
		protected virtual void CallOnDespawned(T entity)
		{
			var poolEntity = entity as IPoolEntityDespawn;

			poolEntity?.OnDespawn();
		}

		// A plain entity == null inside this generic class only ever performs C# reference-equality: T is constrained
		// to class, not to UnityEngine.Object, so the compiler cannot dispatch to UnityEngine.Object's overloaded ==
		// that detects a destroyed-but-not-null ("fake-null") native object. When entity's runtime type IS a
		// UnityEngine.Object (e.g. a pooled GameObject/Behaviour), this dispatches to that overload via a runtime
		// type check instead; for a non-Unity T (a POCO pooled type), it falls back to a plain reference-null check.
		private static bool IsDestroyedOrNull(T entity)
		{
			return entity is UnityEngine.Object unityObject ? unityObject == null : entity == null;
		}
	}

	/// <inheritdoc />
	public class ObjectPool<T> : ObjectPoolBase<T> where T : class
	{
		public ObjectPool(uint initSize, T sampleEntity, Func<T, T> instantiator) : base(initSize, sampleEntity, instantiator)
        {
        }
		
		public ObjectPool(uint initSize, Func<T> instantiator) : base(initSize, instantiator(), entityRef => instantiator.Invoke())
		{
		}
	}
}
