using System.Collections.Concurrent;

namespace ObjectSync.Synchronization
{

	public class StaticSynchronizationAuthority : SynchronizationAuthorityBase
	{
		private class PropertyStateBase
		{
			public static PropertyStateBase Default { get; } = new PropertyStateBase();

			public virtual void Remove(SyncInfo syncInfo) { }
		}
		private sealed class PropertyState<TProperty> : PropertyStateBase
		{
#pragma warning disable CS8618
			private TProperty _value;
#pragma warning restore CS8618
			private readonly SemaphoreSlim _valueGate = new(1, 1);
			private readonly ConcurrentDictionary<String, Action<TProperty>> _callbacks = new();
			private readonly Int32 _degreeOfParallelism = Environment.ProcessorCount > 1 ?
														  Environment.ProcessorCount / 2 :
														  1;

			public TProperty GetValue()
			{
				return _value;
			}
			public void SetValue(SyncInfo syncInfo, TProperty value)
			{
				_valueGate.Wait();
				try
				{
					var exceptions = new List<Exception>();

					var callbackGroups = _callbacks
						.Where(c => c.Key != syncInfo.InstanceId)
						.Select((e, i) => (Group: i % _degreeOfParallelism, Callback: e.Value))
						.GroupBy(e => e.Group, e => e.Callback);

					_ = Parallel.ForEach(callbackGroups, invokeCallbacks);

					if (exceptions.Any())
					{
						throw new AggregateException(exceptions);
					}

					void invokeCallbacks(IEnumerable<Action<TProperty>> callbacks)
					{
						foreach (var callback in callbacks)
						{
							try
							{
								callback.Invoke(value);
							}
							catch (Exception ex)
							{
								exceptions.Add(ex);
							}
						}
					}
				}
				finally
				{
					_value = value;
					_ = _valueGate.Release();
				}
			}
			public void Add(SyncInfo syncInfo, Action<TProperty> callback)
			{
				_ = _callbacks.AddOrUpdate(syncInfo.InstanceId, callback, (key, value) => callback);
			}
			public override void Remove(SyncInfo syncInfo)
			{
				_ = _callbacks.TryRemove(syncInfo.InstanceId, out _);
			}
		}

		//PropertySynchronizationGroupId->PropertyState
		private static readonly ConcurrentDictionary<String, Object> _propertyStates = new();

		private static PropertyState<TProperty> GetPropertyState<TProperty>(SyncInfo syncInfo)
		{
			var context = (PropertyState<TProperty>)_propertyStates.GetOrAdd(syncInfo.PropertyStateId, new PropertyState<TProperty>());

			return context;
		}
		private static PropertyStateBase GetPropertyState(SyncInfo syncInfo)
		{
			var context = _propertyStates.TryGetValue(syncInfo.PropertyStateId, out var state) ?
				(PropertyStateBase)state :
				PropertyStateBase.Default;

			return context;
		}

		protected override void Push<TProperty>(SyncInfo syncInfo, TProperty value)
		{
			var context = GetPropertyState<TProperty>(syncInfo);
			context.SetValue(syncInfo, value);
		}

		protected override void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback)
		{
			var context = GetPropertyState<TProperty>(syncInfo);
			context.Add(syncInfo, callback);
		}

		protected override void Unsubscribe(SyncInfo syncInfo)
		{
			var context = GetPropertyState(syncInfo);
			context.Remove(syncInfo);
		}

		protected override TProperty Pull<TProperty>(SyncInfo syncInfo)
		{
			var context = GetPropertyState<TProperty>(syncInfo);
			var value = context.GetValue();

			return value;
		}
	}
}
