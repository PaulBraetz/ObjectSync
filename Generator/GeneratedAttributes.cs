using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using System;

namespace ObjectSync.Attributes
{
	internal static class Attributes
	{
		/// <summary>
		/// Enumeration for common accessibility combinations. Taken from https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.accessibility?view=roslyn-dotnet-4.3.0
		/// </summary>
		public enum Accessibility
		{
			/// <summary>
			/// No accessibility specified.
			/// </summary>
			NotApplicable = 0,
			Private = 1,
			/// <summary>
			/// Only accessible where both protected and public members are accessible (more
			/// restrictive than <see cref="Protected"/>, <see cref="Internal"/>
			/// and <see cref="ProtectedOrInternal"/>).
			/// </summary>
			//ProtectedAndInternal = 2,
			/// <summary>
			/// Only accessible where both protected and friend members are accessible(more
			/// restrictive than <see cref="Protected"/>, <see cref="Friend"/>
			/// and <see cref="ProtectedOrFriend"/>).
			/// </summary>
			//ProtectedAndFriend = 2,
			Protected = 3,
			Internal = 4,
			//Friend = 4,
			/// <summary>
			/// Accessible wherever either protected or public members are accessible(less
			/// restrictive than <see cref="Protected"/>, <see cref="Internal"/>
			/// and <see cref="ProtectedAndInternal"/>).
			/// </summary>
			ProtectedOrInternal = 5,
			/// <summary>
			/// Accessible wherever either protected or public members are accessible(less
			/// restrictive than <see cref="Protected"/>, <see cref="Friend"/>
			/// and <see cref="ProtectedAndFriend"/>).
			/// </summary>
			//ProtectedOrFriend = 5,
			Public = 6
		}

		public enum Modifier
		{
			/// <summary>
			/// No modifier specified.
			/// </summary>
			NotApplicable,
			Sealed,
			Override,
			Virtual,
			New
		}
	}

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
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	internal sealed class SynchronizedAttribute : Attribute
	{
		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
		public ObjectSync.Attributes.Attributes.Accessibility PropertyAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Public;
	}
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal sealed class SynchronizationTargetAttribute : Attribute
	{
		private String contextPropertyName = "SynchronizationContext";
		private ObjectSync.Attributes.Attributes.Accessibility contextTypeConstructorAccessibility = ObjectSync.Attributes.Attributes.Accessibility.Public;

		public String BaseContextTypeName { get; set; } = null;

		public ObjectSync.Attributes.Attributes.Accessibility ContextTypeAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Private;
		public bool ContextTypeIsSealed { get; set; } = true;
		public ObjectSync.Attributes.Attributes.Accessibility ContextTypeConstructorAccessibility
		{
			get => contextTypeConstructorAccessibility;
			set
			{
				if (value == ObjectSync.Attributes.Attributes.Accessibility.Private)
				{
					throw new ArgumentException($"{nameof(ContextTypeConstructorAccessibility)} cannot be {ObjectSync.Attributes.Attributes.Accessibility.Private}.");
				}

				if (ContextTypeIsSealed && value == ObjectSync.Attributes.Attributes.Accessibility.Protected)
				{
					throw new ArgumentException($"{nameof(ContextTypeConstructorAccessibility)} cannot be {ObjectSync.Attributes.Attributes.Accessibility.Protected} while {ContextTypeIsSealed} is {true}.");
				}

				contextTypeConstructorAccessibility = value;
			}
		}

		public String ContextPropertyName
		{
			get => contextPropertyName;
			set
			{
				if (String.IsNullOrEmpty(value))
				{
					throw new ArgumentException($"{nameof(ContextPropertyName)} cannot be null or empty.");
				}

				contextPropertyName = value;
			}
		}
		public ObjectSync.Attributes.Attributes.Modifier ContextPropertyModifier { get; set; } = ObjectSync.Attributes.Attributes.Modifier.NotApplicable;
		public ObjectSync.Attributes.Attributes.Accessibility ContextPropertyAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Protected;
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	internal sealed class TypeExportConfigurationAttribute : Attribute
	{
		public enum ExportConfig
		{
			Generate,
			Export,
			Import
		}
		public ExportConfig Type { get; set; }
		public string RootNamespace { get; set; }
	}
}

namespace ObjectSync.Generator
{
	internal static class GeneratedAttributes
	{
		#region TypeExportConfiguration
		private const String TypeExportConfiguration_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	internal sealed class TypeExportConfigurationAttribute : Attribute
	{
		public enum ExportConfig
		{
			Generate,
			Export,
			Import
		}
		public ExportConfig Type { get; set; }
		public string RootNamespace { get; set; }
	}
}";
		public static AttributeAnalysisUnit<TypeExportConfigurationAttribute> TypeExportConfiguration { get; } = new AttributeAnalysisUnit<TypeExportConfigurationAttribute>(TypeExportConfiguration_SOURCE);
		#endregion
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
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
}";
		public static AttributeAnalysisUnit<SynchronizationAuthorityAttribute> SynchronizationAuthority { get; } = new AttributeAnalysisUnit<SynchronizationAuthorityAttribute>(SYNCHRONIZATION_AUTHORITY_SOURCE);
		#endregion

		#region SynchronizationTargetAttribute
		private const String SYNCHRONIZATION_TARGET_SOURCE = @"using System;

namespace ObjectSync.Attributes
{	
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal sealed class SynchronizationTargetAttribute : Attribute
	{
		private String contextPropertyName = ""SynchronizationContext"";
		private ObjectSync.Attributes.Attributes.Accessibility contextTypeConstructorAccessibility = ObjectSync.Attributes.Attributes.Accessibility.Public;

		public String BaseContextTypeName { get; set; } = null;

		public ObjectSync.Attributes.Attributes.Accessibility ContextTypeAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Private;
		public bool ContextTypeIsSealed { get; set; } = true;
		public ObjectSync.Attributes.Attributes.Accessibility ContextTypeConstructorAccessibility
		{
			get => contextTypeConstructorAccessibility;
			set
			{
				if (value == ObjectSync.Attributes.Attributes.Accessibility.Private)
				{
					throw new ArgumentException($""{nameof(ContextTypeConstructorAccessibility)} cannot be {Attributes.Accessibility.Private}."");
				}

				if (ContextTypeIsSealed && value == ObjectSync.Attributes.Attributes.Accessibility.Protected)
				{
					throw new ArgumentException($""{nameof(ContextTypeConstructorAccessibility)} cannot be {Attributes.Accessibility.Protected} while {ContextTypeIsSealed} is {true}."");
				}

				contextTypeConstructorAccessibility = value;
			}
		}

		public String ContextPropertyName
		{
			get => contextPropertyName;
			set
			{
				if (String.IsNullOrEmpty(value))
				{
					throw new ArgumentException($""{nameof(ContextPropertyName)} cannot be null or empty."");
				}

				contextPropertyName = value;
			}
		}
		public ObjectSync.Attributes.Attributes.Modifier ContextPropertyModifier { get; set; } = ObjectSync.Attributes.Attributes.Modifier.NotApplicable;
		public ObjectSync.Attributes.Attributes.Accessibility ContextPropertyAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Protected;
	}
}";
		public static AttributeAnalysisUnit<SynchronizationTargetAttribute> SynchronizationTarget { get; } = new AttributeAnalysisUnit<SynchronizationTargetAttribute>(SYNCHRONIZATION_TARGET_SOURCE);
		#endregion

		#region Synchronized
		private const String SYNCHRONIZED_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
	internal sealed class SynchronizedAttribute : Attribute
	{
		public string PropertyName { get; set; }
		public bool Fast { get; set; }
		public bool Observable { get; set; }
		public ObjectSync.Attributes.Attributes.Accessibility PropertyAccessibility { get; set; } = ObjectSync.Attributes.Attributes.Accessibility.Public;
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

		#region Attributes
		private const String Attributes_SOURCE = @"using System;

namespace ObjectSync.Attributes
{
	internal static class Attributes
	{		
		/// <summary>
		/// Enumeration for common accessibility combinations. Taken from https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.accessibility?view=roslyn-dotnet-4.3.0
		/// </summary>
		public enum Accessibility
		{
			/// <summary>
			/// No accessibility specified.
			/// </summary>
			NotApplicable = 0,
			Private = 1,
			/// <summary>
			/// Only accessible where both protected and public members are accessible (more
			/// restrictive than <see cref=""Protected""/>, <see cref=""Internal""/>
			/// and <see cref=""ProtectedOrInternal""/>).
			/// </summary>
			//ProtectedAndInternal = 2,
			/// <summary>
			/// Only accessible where both protected and friend members are accessible(more
			/// restrictive than <see cref=""Protected""/>, <see cref=""Friend""/>
			/// and <see cref=""ProtectedOrFriend""/>).
			/// </summary>
			//ProtectedAndFriend = 2,
			Protected = 3,
			Internal = 4,
			//Friend = 4,
			/// <summary>
			/// Accessible wherever either protected or public members are accessible(less
			/// restrictive than <see cref=""Protected""/>, <see cref=""Internal""/>
			/// and <see cref=""ProtectedAndInternal""/>).
			/// </summary>
			ProtectedOrInternal = 5,
			/// <summary>
			/// Accessible wherever either protected or public members are accessible(less
			/// restrictive than <see cref=""Protected""/>, <see cref=""Friend""/>
			/// and <see cref=""ProtectedAndFriend""/>).
			/// </summary>
			//ProtectedOrFriend = 5,
			Public = 6
		}

		public enum Modifier
		{
			/// <summary>
			/// No modifier specified.
			/// </summary>
			NotApplicable,
			Sealed,
			Override,
			Virtual,
			New
		}
	}
}";
		public static GeneratedType Attributes = new GeneratedType(identifier: TypeIdentifier.Create(
																					TypeIdentifierName.Create().AppendNamePart(nameof(ObjectSync.Attributes.Attributes)),
																					SynchronizationTarget.GeneratedType.Identifier.Namespace),
																	source: Attributes_SOURCE);
		#endregion
	}
}
