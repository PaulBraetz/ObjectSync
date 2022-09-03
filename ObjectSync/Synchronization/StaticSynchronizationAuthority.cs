using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectSync.Synchronization
{
	public class StaticSynchronizationAuthority : SynchronizationAuthorityBase
	{
		private sealed class SynchronizationPropertyContext
		{
			private readonly Int32 _degreeOfParallelism;

			public SynchronizationPropertyContext()
			{
				_degreeOfParallelism = Environment.ProcessorCount > 1 ?
					Environment.ProcessorCount / 2 :
					1;
			}

			private Object _value;
			private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
			private readonly ConcurrentDictionary<String, Action<Object>> _callbacks = new ConcurrentDictionary<string, Action<object>>();

			public TProperty GetValue<TProperty>()
			{
				return (TProperty)_value;
			}
			public async Task SetValue(String instanceId, Object value)
			{
				await _valueGate.WaitAsync();
				try
				{
					var tasks = _callbacks
						.Where(c=>c.Key != instanceId)
						.Select((e, i) => (Group: i % _degreeOfParallelism, Element: e))
						.GroupBy(e => e.Group, e => e.Element)
						.Select(g => Task.Run(() =>
						{
							foreach (var callback in g)
							{
								callback.Value.Invoke(value);
							}
						}));
					await Task.WhenAll(tasks);
				}
				finally
				{
					_value = value;
					_valueGate.Release();
				}
			}
			public void Add(String instanceId, Action<Object> boxedCallback)
			{
				_callbacks.AddOrUpdate(instanceId, boxedCallback, (key, value) => boxedCallback);
			}
			public void Remove(String instanceId)
			{
				_callbacks.TryRemove(instanceId, out var _);
			}
		}

		//synchronizationId+propertyId->(value, instanceId->callback)
		private static readonly ConcurrentDictionary<String, SynchronizationPropertyContext> _subscriptions = new ConcurrentDictionary<string, SynchronizationPropertyContext>();

		private static String GetSubscriptionKey(String synchronizationId, String propertyName)
		{
			return $"{synchronizationId}{propertyName}";
		}
		private SynchronizationPropertyContext GetContext(String synchronizationId, String propertyName)
		{
			var context = _subscriptions.GetOrAdd(GetSubscriptionKey(synchronizationId, propertyName), new SynchronizationPropertyContext());

			return context;
		}

		public override async void Push<TProperty>(String synchronizationId, String propertyName, String instanceId, TProperty value)
		{
			var context = GetContext(synchronizationId, propertyName);
			var awaiter = context.SetValue(instanceId, value).GetAwaiter();
			while (!awaiter.IsCompleted) { }
		}

		public override void Subscribe<TProperty>(String synchronizationId, String propertyName, String instanceId, Action<TProperty> callback)
		{
			Action<Object> boxedCallback = o => callback.Invoke((TProperty)o);
			var context = GetContext(synchronizationId, propertyName);
			context.Add(instanceId, boxedCallback);
		}

		public override void Unsubscribe(String synchronizationId, String propertyName, String instanceId)
		{
			var context = GetContext(synchronizationId, propertyName);
			context.Remove(instanceId);
		}

		public override TProperty Pull<TProperty>(String synchronizationId, String propertyName, String instanceId)
		{
			var context = GetContext(synchronizationId, propertyName);
			var value = context.GetValue<TProperty>();

			return value;
		}
	}
}
