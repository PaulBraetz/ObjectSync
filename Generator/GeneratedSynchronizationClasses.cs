using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using RhoMicro.CodeAnalysis;
using ObjectSync.Synchronization;
using System.Runtime.InteropServices;

namespace ObjectSync.Synchronization
{
	public sealed class Initializable<T> : IEquatable<Initializable<T>>
	{
		public Boolean IsAssigned => _isAssigned == 1;
		public T Value { get; private set; }
		private Int32 _isAssigned;

		public Initializable(T value)
		{
			Assign(value);
		}

		public Initializable()
		{

		}

		public void Assign(T value)
		{
			if (System.Threading.Interlocked.CompareExchange(ref _isAssigned, 1, 0) == 1)
			{
				throw new InvalidOperationException("Cannot initialize multiple times.");
			}

			Value = value;
		}

		public static implicit operator T(Initializable<T> initializable)
		{
			return initializable.Value;
		}

		public override String ToString()
		{
			return Value?.ToString();
		}

		public override Boolean Equals(Object obj)
		{
			return obj is Initializable<T> initializable && Equals(initializable);
		}

		public Boolean Equals(Initializable<T> other)
		{
			return EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		public override Int32 GetHashCode()
		{
			return -1937169414 + EqualityComparer<T>.Default.GetHashCode(Value);
		}

		public static Boolean operator ==(Initializable<T> left, Initializable<T> right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(Initializable<T> left, Initializable<T> right)
		{
			return !(left == right);
		}
	}
	public interface ISynchronizationAuthority
	{
		TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId);
		void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value);
		void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback);
		void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId);
	}
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
			private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
			private readonly ConcurrentDictionary<String, Action<TProperty>> _callbacks = new ConcurrentDictionary<string, Action<TProperty>>();
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

		protected StaticSynchronizationAuthority() { }

		public static readonly StaticSynchronizationAuthority Instance = new StaticSynchronizationAuthority();

		//PropertySynchronizationGroupId->PropertyState
		private static readonly ConcurrentDictionary<String, Object> _propertyStates = new ConcurrentDictionary<string, object>();

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
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		protected abstract TProperty Pull<TProperty>(SyncInfo syncInfo);
		public TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			return Pull<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}

		protected abstract void Push<TProperty>(SyncInfo syncInfo, TProperty value);
		public void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value)
		{
			Push<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), value);
		}

		protected abstract void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback);
		public void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback)
		{
			Subscribe<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), callback);
		}

		protected abstract void Unsubscribe(SyncInfo syncInfo);
		public void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			Unsubscribe(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}
	}
	public readonly struct SyncInfo : IEquatable<SyncInfo>
	{
		public readonly String TypeId;
		public readonly String PropertyName;
		public readonly String SourceInstanceId;
		public readonly String InstanceId;

		public readonly String PropertyStateId;

		public SyncInfo(String typeId, String propertyName, String sourceInstanceId, String instanceId) : this()
		{
			TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
			PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
			SourceInstanceId = sourceInstanceId ?? throw new ArgumentNullException(nameof(sourceInstanceId));
			InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

			PropertyStateId = $"{TypeId}.{PropertyName}[{SourceInstanceId}]";
		}

		public override Boolean Equals(Object obj)
		{
			return obj is SyncInfo info && Equals(info);
		}

		public Boolean Equals(SyncInfo other)
		{
			return InstanceId == other.InstanceId &&
				   PropertyStateId == other.PropertyStateId;
		}

		public override Int32 GetHashCode()
		{
			var hashCode = -1129730193;
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(InstanceId);
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(PropertyStateId);
			return hashCode;
		}

		public static Boolean operator ==(SyncInfo left, SyncInfo right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(SyncInfo left, SyncInfo right)
		{
			return !(left == right);
		}
	}
}

namespace ObjectSync.Generator
{
	internal static class GeneratedSynchronizationClasses
	{
		#region Initializable
		private const string Initializable_SOURCE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ObjectSync.Synchronization
{
	public sealed class Initializable<T> : IEquatable<Initializable<T>>
	{
		public Boolean IsAssigned => _isAssigned == 1;
		public T Value { get; private set; }
		private Int32 _isAssigned;

		public Initializable(T value)
		{
			Assign(value);
		}

		public Initializable()
		{

		}

		public void Assign(T value)
		{
			if (System.Threading.Interlocked.CompareExchange(ref _isAssigned, 1, 0) == 1)
			{
				throw new InvalidOperationException(""Cannot initialize multiple times."");
			}

			Value = value;
		}

		public override String ToString()
		{
			return Value?.ToString();
		}

		public static implicit operator T(Initializable<T> initializable)
		{
			return initializable.Value;
		}

		public override Boolean Equals(Object obj)
		{
			return obj is Initializable<T> initializable && Equals(initializable);
		}

		public Boolean Equals(Initializable<T> other)
		{
			return EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		public override Int32 GetHashCode()
		{
			return -1937169414 + EqualityComparer<T>.Default.GetHashCode(Value);
		}

		public static Boolean operator ==(Initializable<T> left, Initializable<T> right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(Initializable<T> left, Initializable<T> right)
		{
			return !(left == right);
		}
	}
}";
		public static GeneratedType Initializable { get; } = new GeneratedType(
			TypeIdentifier.Create(
				TypeIdentifierName.Create()
					.AppendNamePart("Initializable"),
				Namespace.Create()
					.Append("ObjectSync")
					.Append("Synchronization")),
			new GeneratedSource(Initializable_SOURCE, "ObjectSync.Synchronization.Initializable"));
		#endregion

		#region ISynchronizationAuthority
		private const string ISynchronizationAuthority_SOURCE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
	{
		TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId);
		void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value);
		void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback);
		void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId);
	}
}";
		public static GeneratedType ISynchronizationAuthority { get; } = new GeneratedType(TypeIdentifier.Create<ISynchronizationAuthority>(), ISynchronizationAuthority_SOURCE);
		#endregion

		#region StaticSynchronizationAuthority
		private const string StaticSynchronizationAuthority_SOURCE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

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
			private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
			private readonly ConcurrentDictionary<String, Action<TProperty>> _callbacks = new ConcurrentDictionary<string, Action<TProperty>>();
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

		protected StaticSynchronizationAuthority() { }

		public static readonly StaticSynchronizationAuthority Instance = new StaticSynchronizationAuthority();

		//PropertySynchronizationGroupId->PropertyState
		private static readonly ConcurrentDictionary<String, Object> _propertyStates = new ConcurrentDictionary<string, object>();

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
}";
		public static GeneratedType StaticSynchronizationAuthority { get; } = new GeneratedType(TypeIdentifier.Create<StaticSynchronizationAuthority>(), StaticSynchronizationAuthority_SOURCE);
		#endregion

		#region SynchronizationAuthorityBase
		private const string SynchronizationAuthorityBase_SOURCE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ObjectSync.Synchronization
{
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		protected abstract TProperty Pull<TProperty>(SyncInfo syncInfo);
		public TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			return Pull<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}

		protected abstract void Push<TProperty>(SyncInfo syncInfo, TProperty value);
		public void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value)
		{
			Push<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), value);
		}

		protected abstract void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback);
		public void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback)
		{
			Subscribe<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), callback);
		}

		protected abstract void Unsubscribe(SyncInfo syncInfo);
		public void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			Unsubscribe(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}
	}
}";
		public static GeneratedType SynchronizationAuthorityBase { get; } = new GeneratedType(TypeIdentifier.Create<SynchronizationAuthorityBase>(), SynchronizationAuthorityBase_SOURCE);
		#endregion

		#region SyncInfo
		private const string SyncInfo_SOURCE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ObjectSync.Synchronization
{
	public readonly struct SyncInfo : IEquatable<SyncInfo>
	{
		public readonly String TypeId;
		public readonly String PropertyName;
		public readonly String SourceInstanceId;
		public readonly String InstanceId;

		public readonly String PropertyStateId;

		public SyncInfo(String typeId, String propertyName, String sourceInstanceId, String instanceId) : this()
		{
			TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
			PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
			SourceInstanceId = sourceInstanceId ?? throw new ArgumentNullException(nameof(sourceInstanceId));
			InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

			PropertyStateId = $""{TypeId}.{PropertyName}[{SourceInstanceId}]"";
		}

		public override Boolean Equals(Object obj)
		{
			return obj is SyncInfo info && Equals(info);
		}

		public Boolean Equals(SyncInfo other)
		{
			return InstanceId == other.InstanceId &&
				   PropertyStateId == other.PropertyStateId;
		}

		public override Int32 GetHashCode()
		{
			var hashCode = -1129730193;
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(InstanceId);
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(PropertyStateId);
			return hashCode;
		}

		public static Boolean operator ==(SyncInfo left, SyncInfo right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(SyncInfo left, SyncInfo right)
		{
			return !(left == right);
		}
	}
}";
		public static GeneratedType SyncInfo { get; } = new GeneratedType(TypeIdentifier.Create<SyncInfo>(), SyncInfo_SOURCE);
		#endregion
	}
}
