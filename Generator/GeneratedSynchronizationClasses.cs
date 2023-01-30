using RhoMicro.CodeAnalysis;
using RhoMicro.ObjectSync.Attributes;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RhoMicro.ObjectSync.Generator
{
    public sealed class Initializable<T> : IEquatable<Initializable<T>>
    {
        public Boolean IsAssigned => _isAssigned == 1;
        public T Value
        {
            get; private set;
        }
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
            if(Interlocked.CompareExchange(ref _isAssigned, 1, 0) == 1)
            {
                throw new InvalidOperationException("Cannot initialize multiple times.");
            }

            Value = value;
        }

        public static implicit operator T(Initializable<T> initializable) => initializable.Value;

        public override String ToString() => Value?.ToString();

        public override Boolean Equals(Object obj) => obj is Initializable<T> initializable && Equals(initializable);

        public Boolean Equals(Initializable<T> other) => EqualityComparer<T>.Default.Equals(Value, other.Value);

        public override Int32 GetHashCode() => -1937169414 + EqualityComparer<T>.Default.GetHashCode(Value);

        public static Boolean operator ==(Initializable<T> left, Initializable<T> right) => left.Equals(right);

        public static Boolean operator !=(Initializable<T> left, Initializable<T> right) => !(left == right);
    }
    public interface ISynchronizationAuthority
    {
        TProperty Pull<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId);
        void Push<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, TProperty value);
        void Subscribe<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, Action<TProperty> callback);
        void Unsubscribe(String typeId, String fieldName, String sourceInstanceId, String instanceId);
    }
    public class StaticSynchronizationAuthority : SynchronizationAuthorityBase
    {
        private class FieldStateContextBase
        {
            public static FieldStateContextBase Default { get; } = new FieldStateContextBase();
            public virtual Boolean IsEmpty => false;
            public virtual void Remove(SyncInfo syncInfo)
            {
            }
        }
        private sealed class FieldStateContext<TField> : FieldStateContextBase
        {
            private TField _value;
            private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
            private readonly ConcurrentDictionary<String, Action<TField>> _callbacks = new ConcurrentDictionary<String, Action<TField>>();
            private readonly Int32 _degreeOfParallelism = Environment.ProcessorCount > 1 ?
                                                          Environment.ProcessorCount / 2 :
                                                          1;
            public override Boolean IsEmpty => !_callbacks.Any();
            public TField GetValue() => _value;
            public void SetValue(SyncInfo syncInfo, TField value)
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

                    if(exceptions.Any())
                    {
                        throw new AggregateException(exceptions);
                    }

                    void invokeCallbacks(IEnumerable<Action<TField>> callbacks)
                    {
                        foreach(var callback in callbacks)
                        {
                            try
                            {
                                callback.Invoke(value);
                            } catch(Exception ex)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }
                } finally
                {
                    _value = value;
                    _ = _valueGate.Release();
                }
            }
            public void Add(SyncInfo syncInfo, Action<TField> callback) => _ = _callbacks.AddOrUpdate(syncInfo.InstanceId, callback, (key, value) => callback);
            public override void Remove(SyncInfo syncInfo) => _ = _callbacks.TryRemove(syncInfo.InstanceId, out _);
        }

        protected StaticSynchronizationAuthority()
        {
        }

        public static readonly StaticSynchronizationAuthority Instance = new StaticSynchronizationAuthority();

        //PropertySynchronizationGroupId->PropertyState
        private static readonly ConcurrentDictionary<String, Object> _fieldStates = new ConcurrentDictionary<String, Object>();

        private static FieldStateContext<TField> GetFieldContext<TField>(SyncInfo syncInfo)
        {
            var context = (FieldStateContext<TField>)_fieldStates.GetOrAdd(syncInfo.FieldStateId, new FieldStateContext<TField>());

            return context;
        }
        private static FieldStateContextBase GetFieldContext(SyncInfo syncInfo)
        {
            var context = _fieldStates.TryGetValue(syncInfo.FieldStateId, out var state) ?
                (FieldStateContextBase)state :
                FieldStateContextBase.Default;

            return context;
        }
        private static void EnsureFieldContextNotEmpty(SyncInfo syncInfo, FieldStateContextBase state)
        {
            if(state.IsEmpty)
            {
                _ = _fieldStates.TryRemove(syncInfo.FieldStateId, out _);
            }
        }

        protected override void Push<TField>(SyncInfo syncInfo, TField value)
        {
            var context = GetFieldContext<TField>(syncInfo);
            context.SetValue(syncInfo, value);
        }

        protected override void Subscribe<TField>(SyncInfo syncInfo, Action<TField> callback)
        {
            var context = GetFieldContext<TField>(syncInfo);
            context.Add(syncInfo, callback);
        }

        protected override void Unsubscribe(SyncInfo syncInfo)
        {
            var context = GetFieldContext(syncInfo);
            context.Remove(syncInfo);
            EnsureFieldContextNotEmpty(syncInfo, context);
        }

        protected override TField Pull<TField>(SyncInfo syncInfo)
        {
            var context = GetFieldContext<TField>(syncInfo);
            var value = context.GetValue();
            EnsureFieldContextNotEmpty(syncInfo, context);

            return value;
        }
    }
    public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
    {
        protected abstract TProperty Pull<TProperty>(SyncInfo syncInfo);
        public TProperty Pull<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId) => Pull<TProperty>(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId));

        protected abstract void Push<TProperty>(SyncInfo syncInfo, TProperty value);
        public void Push<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, TProperty value) => Push(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId), value);

        protected abstract void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback);
        public void Subscribe<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, Action<TProperty> callback) => Subscribe(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId), callback);

        protected abstract void Unsubscribe(SyncInfo syncInfo);
        public void Unsubscribe(String typeId, String fieldName, String sourceInstanceId, String instanceId) => Unsubscribe(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId));
    }
    public readonly struct SyncInfo : IEquatable<SyncInfo>
    {
        public String TypeId { get; }
        public String FieldName { get; }
        public String SourceInstanceId { get; }
        public String InstanceId { get; }
        public String FieldStateId { get; }

        public SyncInfo(String typeId, String fieldName, String sourceInstanceId, String instanceId) : this()
        {
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            SourceInstanceId = sourceInstanceId ?? throw new ArgumentNullException(nameof(sourceInstanceId));
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

            FieldStateId = $"{TypeId}.{FieldName}[{SourceInstanceId}]";
        }

        public override Boolean Equals(Object obj) => obj is SyncInfo info && Equals(info);

        public Boolean Equals(SyncInfo other)
        {
            return InstanceId == other.InstanceId &&
                   FieldStateId == other.FieldStateId;
        }

        public override Int32 GetHashCode()
        {
            var hashCode = -1129730193;
            hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(InstanceId);
            hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(FieldStateId);
            return hashCode;
        }

        public static Boolean operator ==(SyncInfo left, SyncInfo right) => left.Equals(right);

        public static Boolean operator !=(SyncInfo left, SyncInfo right) => !(left == right);
    }
}

namespace RhoMicro.ObjectSync.Generator
{
    internal static class GeneratedSynchronizationClasses
    {
        #region Initializable
        private const String INITIALIZABLE_SOURCE_TEMPLATE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace " + NAMESPACE_PLACEHOLDER + @"
{
	" + ACCESSIBILITY_PLACEHOLDER + @" sealed class Initializable<T> : IEquatable<Initializable<T>>
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
			if (Interlocked.CompareExchange(ref _isAssigned, 1, 0) == 1)
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
        public static GeneratedType GetInitializable(TypeExportConfigurationAttribute config)
            => GetGeneratedType(
                config,
                INITIALIZABLE_SOURCE_TEMPLATE,
                TypeIdentifierName.Create()
                    .AppendNamePart("Initializable"));
        #endregion

        #region ISynchronizationAuthority
        private const String ISYNCHRONIZATIONAUTHORITY_SOURCE_TEMPLATE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace " + NAMESPACE_PLACEHOLDER + @"
{
	" + ACCESSIBILITY_PLACEHOLDER + @" interface ISynchronizationAuthority
	{
		TProperty Pull<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId);
		void Push<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, TProperty value);
		void Subscribe<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, Action<TProperty> callback);
		void Unsubscribe(String typeId, String fieldName, String sourceInstanceId, String instanceId);
	}
}";
        public static GeneratedType GetISynchronizationAuthority(TypeExportConfigurationAttribute config)
            => GetGeneratedType<ISynchronizationAuthority>(config, ISYNCHRONIZATIONAUTHORITY_SOURCE_TEMPLATE);
        #endregion

        #region StaticSynchronizationAuthority
        private const String STATICSYNCHRONIZATIONAUTHORITY_SOURCE_TEMPLATE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace " + NAMESPACE_PLACEHOLDER + @"
{
	" + ACCESSIBILITY_PLACEHOLDER + @" class StaticSynchronizationAuthority : SynchronizationAuthorityBase
	{
		private class FieldStateContextBase
		{
			public static FieldStateContextBase Default { get; } = new FieldStateContextBase();

			public virtual void Remove(SyncInfo syncInfo) { }
		}
		private sealed class FieldStateContext<TField> : FieldStateContextBase
		{
#pragma warning disable CS8618
			private TField _value;
#pragma warning restore CS8618
			private readonly SemaphoreSlim _valueGate = new SemaphoreSlim(1, 1);
			private readonly ConcurrentDictionary<String, Action<TField>> _callbacks = new ConcurrentDictionary<string, Action<TField>>();
			private readonly Int32 _degreeOfParallelism = Environment.ProcessorCount > 1 ?
														  Environment.ProcessorCount / 2 :
														  1;

			public TField GetValue()
			{
				return _value;
			}
			public void SetValue(SyncInfo syncInfo, TField value)
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

					void invokeCallbacks(IEnumerable<Action<TField>> callbacks)
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
			public void Add(SyncInfo syncInfo, Action<TField> callback)
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
		private static readonly ConcurrentDictionary<String, Object> _fieldStates = new ConcurrentDictionary<String, Object>();

		private static FieldStateContext<TField> GetFieldContext<TField>(SyncInfo syncInfo)
		{
			var context = (FieldStateContext<TField>)_fieldStates.GetOrAdd(syncInfo.FieldStateId, new FieldStateContext<TField>());

			return context;
		}
		private static FieldStateContextBase GetFieldContext(SyncInfo syncInfo)
		{
			var context = _fieldStates.TryGetValue(syncInfo.FieldStateId, out var state) ?
				(FieldStateContextBase)state :
				FieldStateContextBase.Default;

			return context;
		}

		protected override void Push<TField>(SyncInfo syncInfo, TField value)
		{
			var context = GetFieldContext<TField>(syncInfo);
			context.SetValue(syncInfo, value);
		}

		protected override void Subscribe<TField>(SyncInfo syncInfo, Action<TField> callback)
		{
			var context = GetFieldContext<TField>(syncInfo);
			context.Add(syncInfo, callback);
		}

		protected override void Unsubscribe(SyncInfo syncInfo)
		{
			var context = GetFieldContext(syncInfo);
			context.Remove(syncInfo);
		}

		protected override TField Pull<TField>(SyncInfo syncInfo)
		{
			var context = GetFieldContext<TField>(syncInfo);
			var value = context.GetValue();

			return value;
		}
	}
}";
        public static GeneratedType GetStaticSynchronizationAuthority(TypeExportConfigurationAttribute config)
            => GetGeneratedType<StaticSynchronizationAuthority>(config, STATICSYNCHRONIZATIONAUTHORITY_SOURCE_TEMPLATE);
        #endregion

        #region SynchronizationAuthorityBase
        private const String SYNCHRONIZATIONAUTHORITYBASE_SOURCE_TEMPLATE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace " + NAMESPACE_PLACEHOLDER + @"
{
	" + ACCESSIBILITY_PLACEHOLDER + @" abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		protected abstract TProperty Pull<TProperty>(SyncInfo syncInfo);
		public TProperty Pull<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId)
		{
			return Pull<TProperty>(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId));
		}

		protected abstract void Push<TProperty>(SyncInfo syncInfo, TProperty value);
		public void Push<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, TProperty value)
		{
			Push<TProperty>(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId), value);
		}

		protected abstract void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback);
		public void Subscribe<TProperty>(String typeId, String fieldName, String sourceInstanceId, String instanceId, Action<TProperty> callback)
		{
			Subscribe<TProperty>(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId), callback);
		}

		protected abstract void Unsubscribe(SyncInfo syncInfo);
		public void Unsubscribe(String typeId, String fieldName, String sourceInstanceId, String instanceId)
		{
			Unsubscribe(new SyncInfo(typeId, fieldName, sourceInstanceId, instanceId));
		}
	}
}";
        public static GeneratedType GetSynchronizationAuthorityBase(TypeExportConfigurationAttribute config)
            => GetGeneratedType<SynchronizationAuthorityBase>(config, SYNCHRONIZATIONAUTHORITYBASE_SOURCE_TEMPLATE);
        #endregion

        #region SyncInfo
        private const String SYNCINFO_SOURCE_TEMPLATE =
@"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace " + NAMESPACE_PLACEHOLDER + @"
{
	" + ACCESSIBILITY_PLACEHOLDER + @" readonly struct SyncInfo : IEquatable<SyncInfo>
	{
		public readonly String TypeId;
		public readonly String FieldName;
		public readonly String SourceInstanceId;
		public readonly String InstanceId;

		public readonly String FieldStateId;

		public SyncInfo(String typeId, String fieldName, String sourceInstanceId, String instanceId) : this()
		{
			TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
			FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
			SourceInstanceId = sourceInstanceId ?? throw new ArgumentNullException(nameof(sourceInstanceId));
			InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

			FieldStateId = $""{TypeId}.{FieldName}[{SourceInstanceId}]"";
		}

		public override Boolean Equals(Object obj)
		{
			return obj is SyncInfo info && Equals(info);
		}

		public Boolean Equals(SyncInfo other)
		{
			return InstanceId == other.InstanceId &&
				   FieldStateId == other.FieldStateId;
		}

		public override Int32 GetHashCode()
		{
			var hashCode = -1129730193;
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(InstanceId);
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(FieldStateId);
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
        private const String NAMESPACE_PLACEHOLDER = "{NAMESPACE}";
        private const String ACCESSIBILITY_PLACEHOLDER = "{ACCESSIBILITY}";

        public static GeneratedType GetSyncInfo(TypeExportConfigurationAttribute config)
            => GetGeneratedType<SyncInfo>(config, SYNCINFO_SOURCE_TEMPLATE);
        #endregion

        private static String FormatTemplate(TypeExportConfigurationAttribute config, String template)
        {
            if(config.Type == ExportConfigType.Import)
            {
                return String.Empty;
            }

            var rootNamespace = config.GetSynchronizationNamespace();
            var accessibility = config.Type == ExportConfigType.Export ? "public" : "internal";
            var result = template.Replace(NAMESPACE_PLACEHOLDER, rootNamespace).Replace(ACCESSIBILITY_PLACEHOLDER, accessibility);

            return result;
        }
        private static GeneratedType GetGeneratedType<T>(TypeExportConfigurationAttribute config, String template)
        {
            var name = TypeIdentifierName.Create<T>();
            var generatedType = GetGeneratedType(config, template, name);

            return generatedType;
        }
        private static GeneratedType GetGeneratedType(TypeExportConfigurationAttribute config, String template, TypeIdentifierName name)
        {
            var source = FormatTemplate(config, template);

            var identifier = config.GetSynchronizationType(name);
            var generatedSource = config.Type == ExportConfigType.Import ?
                default :
                new GeneratedSource(source, identifier);

            var generatedType = new GeneratedType(identifier, generatedSource);

            return generatedType;
        }
    }
}
