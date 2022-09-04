using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectSync.Synchronization
{
	public class StaticSynchronizationAuthority : SynchronizationAuthorityBase
	{
		private abstract class SynchronizationPropertyContextBase
		{
			public abstract void Remove(String instanceId);
		}
		private sealed class SynchronizationPropertyContext<TProperty> : SynchronizationPropertyContextBase
		{
			private TProperty _value;
			private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
			private readonly ConcurrentDictionary<String, Action<TProperty>> _callbacks = new ConcurrentDictionary<String, Action<TProperty>>();
			private readonly Int32 _degreeOfParallelism = Environment.ProcessorCount > 1 ?
														  Environment.ProcessorCount / 2 :
														  1;

			public TProperty GetValue()
			{
				return _value;
			}
			public void SetValue(String instanceId, TProperty value)
			{
				_valueGate.Wait();
				try
				{
					var exceptions = new List<Exception>();

					var callbackGroups = _callbacks
						.Where(c => c.Key != instanceId)
						.Select((e, i) => (Group: i % _degreeOfParallelism, Callback: e.Value))
						.GroupBy(e => e.Group, e => e.Callback);

					Parallel.ForEach(callbackGroups, invokeCallbacks);

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
					_valueGate.Release();
				}
			}
			public void Add(String instanceId, Action<TProperty> callback)
			{
				_callbacks.AddOrUpdate(instanceId, callback, (key, value) => callback);
			}
			public override void Remove(String instanceId)
			{
				_callbacks.TryRemove(instanceId, out var _);
			}
		}

		//synchronizationId+propertyId->Context
		private static readonly ConcurrentDictionary<String, Object> _propertyContexts = new ConcurrentDictionary<string, Object>();

		private static String GetSubscriptionKey(String synchronizationId, String propertyName)
		{
			return $"{synchronizationId}{propertyName}";
		}
		private SynchronizationPropertyContext<TProperty> GetContext<TProperty>(String synchronizationId, String propertyName)
		{
			var context = (SynchronizationPropertyContext<TProperty>)_propertyContexts.GetOrAdd(GetSubscriptionKey(synchronizationId, propertyName), new SynchronizationPropertyContext<TProperty>());

			return context;
		}
		private SynchronizationPropertyContextBase GetContext(String synchronizationId, String propertyName)
		{
			var context = (SynchronizationPropertyContextBase)_propertyContexts[GetSubscriptionKey(synchronizationId, propertyName)];

			return context;
		}

		public override void Push<TProperty>(String synchronizationId, String propertyName, String instanceId, TProperty value)
		{
			var context = GetContext<TProperty>(synchronizationId, propertyName);
			context.SetValue(instanceId, value);
		}

		public override void Subscribe<TProperty>(String synchronizationId, String propertyName, String instanceId, Action<TProperty> callback)
		{
			var context = GetContext<TProperty>(synchronizationId, propertyName);
			context.Add(instanceId, callback);
		}

		public override void Unsubscribe(String synchronizationId, String propertyName, String instanceId)
		{
			var context = GetContext(synchronizationId, propertyName);
			context.Remove(instanceId);
		}

		public override TProperty Pull<TProperty>(String synchronizationId, String propertyName, String instanceId)
		{
			var context = GetContext<TProperty>(synchronizationId, propertyName);
			var value = context.GetValue();

			return value;
		}
	}
}
