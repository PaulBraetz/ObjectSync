using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis.Attributes;
using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class TypeIdAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class InstanceIdAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class SourceInstanceIdAttribute : Attribute
	{
		public string PropertyName { get; set; }
	}
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class SynchronizedAttribute : Attribute
	{
		public SynchronizedAttribute(string propertyName = null, bool fast = false, bool observable = false)
		{
			PropertyName = propertyName;
			Fast = fast;
			Observable = observable;
		}

		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
	}
}

namespace ObjectSync.Generator
{
	internal static class GeneratedAttributes
	{
		#region InstanceId
		private const String INSTANCE_ID_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
    public sealed class InstanceIdAttribute : Attribute
    {

	}
}";
		public static AttributeAnalysisUnit<InstanceIdAttribute> InstanceId { get; } = new AttributeAnalysisUnit<InstanceIdAttribute>(INSTANCE_ID_SOURCE);
		#endregion

		#region SourceInstanceId
		private const String SOURCE_INSTANCE_ID_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class SourceInstanceIdAttribute : Attribute
	{
		public string PropertyName { get; set; }
	}
}";
		public static AttributeAnalysisUnit<SourceInstanceIdAttribute> SourceInstanceId { get; } = new AttributeAnalysisUnit<SourceInstanceIdAttribute>(SOURCE_INSTANCE_ID_SOURCE);
		#endregion

		#region SynchronizationAuthority
		private const String SYNCHRONIZATION_AUTHORITY_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	/// <summary>
	/// <para>
	/// Denotes the synchronization authority for synchronized fields of this type. 
	/// The type returned must provide the following methods:
	/// </para>
	/// <para>
	/// <c>TProperty Pull&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId)</c>
	/// </para>
	/// <para>
	/// <c>void Push&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value)</c>
	/// </para>
	/// <para>
	/// <c>void Subscribe&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action&lt;TProperty&gt; callback)</c>
	/// </para>
	/// <para>
	/// <c>void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId)</c>
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class SynchronizationAuthorityAttribute : Attribute
    {

    }
}";
		public static AttributeAnalysisUnit<SynchronizationAuthorityAttribute> SynchronizationAuthority { get; } = new AttributeAnalysisUnit<SynchronizationAuthorityAttribute>(SYNCHRONIZATION_AUTHORITY_SOURCE);
		#endregion

		#region Synchronized
		private const String SYNCHRONIZED_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class SynchronizedAttribute : Attribute
    {
		public SynchronizedAttribute(string propertyName = null, bool fast = false, bool observable = false)
		{
			PropertyName = propertyName;
			Fast = fast;
			Observable = observable;
		}

		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
	}
}";
		public static AttributeAnalysisUnit<SynchronizedAttribute> Synchronized { get; } = new AttributeAnalysisUnit<SynchronizedAttribute>(SYNCHRONIZED_SOURCE);
		#endregion

		#region TypeId
		private const String TYPE_ID_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class TypeIdAttribute : Attribute
	{

	}
}";
		public static AttributeAnalysisUnit<TypeIdAttribute> TypeId { get; } = new AttributeAnalysisUnit<TypeIdAttribute>(TYPE_ID_SOURCE);
		#endregion
	}
}
