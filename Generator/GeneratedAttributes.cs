using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis.Attributes;
using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class TypeIdAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class InstanceIdAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class SourceInstanceIdAttribute : Attribute
	{
		public string PropertyName { get; set; }
	}
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	internal sealed class SynchronizedAttribute : Attribute
	{
		public enum Accessibility
		{
			Public,
			Protected,
			Private
		}

		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
		public Accessibility PropertyAccessibility { get; set; } = Accessibility.Public;
	}
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal sealed class SynchronizationContextAttribute : Attribute
	{
		public enum Accessibility
		{
			Public,
			Protected,
			Private
		}
		public enum Modifier
		{
			None,
			Overrides,
			Virtual,
			New
		}
		public enum TypeModifier
		{
			None,
			Sealed
		}

		public Type BaseContextType { get; set; }
		public Boolean IsSealed { get; set; }
		public Accessibility TypeAccessibility { get; set; } = Accessibility.Protected;
		public TypeModifier TypeModifier { get; set; } = Accessibility.Protected;
		public Modifier PropertyModifier { get; set; }
		public Accessibility PropertyAccessibility { get; set; } = Accessibility.Protected;
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
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class InstanceIdAttribute : Attribute
	{

	}
}";
		public static AttributeAnalysisUnit<InstanceIdAttribute> InstanceId { get; } = new AttributeAnalysisUnit<InstanceIdAttribute>(INSTANCE_ID_SOURCE);
		#endregion

		#region SourceInstanceId
		private const String SOURCE_INSTANCE_ID_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class SourceInstanceIdAttribute : Attribute
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
	internal sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
}";
		public static AttributeAnalysisUnit<SynchronizationAuthorityAttribute> SynchronizationAuthority { get; } = new AttributeAnalysisUnit<SynchronizationAuthorityAttribute>(SYNCHRONIZATION_AUTHORITY_SOURCE);
		#endregion

		#region SynchronizationContextAttribute
		private const String SYNCHRONIZATION_CONTEXT_SOURCE = @"using System;

namespace ObjectSync.Attributes
{	
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal sealed class SynchronizationContextAttribute : Attribute
	{
		public enum Accessibility
		{
			Public,
			Protected,
			Private
		}
		public enum Modifier
		{
			None,
			Overrides,
			Virtual,
			New
		}

		public Type BaseContextType { get; set; }
		public Boolean IsSealed { get; set; }
		public Accessibility TypeAccessibility { get; set; } = Accessibility.Protected;
		public Modifier PropertyModifier { get; set; }
		public Accessibility PropertyAccessibility { get; set; } = Accessibility.Protected;
	}
}";
		public static AttributeAnalysisUnit<SynchronizationContextAttribute> SynchronizationContext { get; } = new AttributeAnalysisUnit<SynchronizationContextAttribute>(SYNCHRONIZATION_CONTEXT_SOURCE);
		#endregion

		#region Synchronized
		private const String SYNCHRONIZED_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
	internal sealed class SynchronizedAttribute : Attribute
	{
		public enum Accessibility
		{
			Public,
			Protected,
			Private
		}

		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
		public Accessibility PropertyAccessibility { get; set; } = Accessibility.Public;
	}
}";
		public static AttributeAnalysisUnit<SynchronizedAttribute> Synchronized { get; } = new AttributeAnalysisUnit<SynchronizedAttribute>(SYNCHRONIZED_SOURCE);
		#endregion

		#region TypeId
		private const String TYPE_ID_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class TypeIdAttribute : Attribute
	{

	}
}";
		public static AttributeAnalysisUnit<TypeIdAttribute> TypeId { get; } = new AttributeAnalysisUnit<TypeIdAttribute>(TYPE_ID_SOURCE);
		#endregion
	}
}
