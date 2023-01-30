using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using RhoMicro.ObjectSync.Attributes;

using System;

namespace RhoMicro.ObjectSync.Attributes
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
        public String PropertyName
        {
            get; set;
        }
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

    public enum ExportConfigType
    {
        Generate,
        Export,
        Import
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    internal sealed class TypeExportConfigurationAttribute : Attribute
    {
        public ExportConfigType Type
        {
            get; set;
        }
        public String RootNamespace
        {
            get; set;
        } = "RhoMicro";
    }

    /// <summary>
    /// Enumeration for common accessibility combinations. Taken from https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.accessibility?view=roslyn-dotnet-4.3.0
    /// </summary>
    internal enum Accessibility
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
    internal enum Modifier
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

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    internal sealed class SynchronizedAttribute : Attribute
    {
        public String PropertyName
        {
            get; set;
        }
        public Boolean Fast
        {
            get; set;
        }
        public Boolean Observable
        {
            get; set;
        }
        public Accessibility PropertyAccessibility { get; set; } = Accessibility.Public;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class SynchronizationTargetAttribute : Attribute
    {
        private String _contextPropertyName = "SynchronizationContext";
        private Accessibility _contextTypeConstructorAccessibility = Accessibility.Public;

        public String BaseContextTypeName { get; set; } = null;

        public Accessibility ContextTypeAccessibility { get; set; } = Accessibility.Private;
        public Boolean ContextTypeIsSealed { get; set; } = true;
        public Accessibility ContextTypeConstructorAccessibility
        {
            get => _contextTypeConstructorAccessibility;
            set
            {
                if(value == Accessibility.Private)
                {
                    throw new ArgumentException($"{nameof(ContextTypeConstructorAccessibility)} cannot be {Accessibility.Private}.");
                }

                if(ContextTypeIsSealed && value == Accessibility.Protected)
                {
                    throw new ArgumentException($"{nameof(ContextTypeConstructorAccessibility)} cannot be {Accessibility.Protected} while {ContextTypeIsSealed} is {true}.");
                }

                _contextTypeConstructorAccessibility = value;
            }
        }

        public String ContextPropertyName
        {
            get => _contextPropertyName;
            set
            {
                if(String.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"{nameof(ContextPropertyName)} cannot be null or empty.");
                }

                _contextPropertyName = value;
            }
        }
        public Modifier ContextPropertyModifier { get; set; } = Modifier.NotApplicable;
        public Accessibility ContextPropertyAccessibility { get; set; } = Accessibility.Protected;
    }
}

namespace RhoMicro.ObjectSync.Generator
{
    internal static class GeneratedAttributes
    {
        #region InstanceId
        private const String INSTANCE_ID_SOURCE = @"using System;

namespace RhoMicro.ObjectSync.Attributes
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

namespace RhoMicro.ObjectSync.Attributes
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

namespace RhoMicro.ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
}";
        public static AttributeAnalysisUnit<SynchronizationAuthorityAttribute> SynchronizationAuthority { get; } = new AttributeAnalysisUnit<SynchronizationAuthorityAttribute>(SYNCHRONIZATION_AUTHORITY_SOURCE);
        #endregion

        #region TypeId
        private const String TYPE_ID_SOURCE = @"using System;

namespace RhoMicro.ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	internal sealed class TypeIdAttribute : Attribute
	{

	}
}";
        public static AttributeAnalysisUnit<TypeIdAttribute> TypeId { get; } = new AttributeAnalysisUnit<TypeIdAttribute>(TYPE_ID_SOURCE);
        #endregion

        #region ExportConfigType
        private const String EXPORTCONFIGTYPE_SOURCE =
@"namespace RhoMicro.ObjectSync.Attributes
{
	public enum ExportConfigType
	{
		Generate,
		Export,
		Import
	}
}";
        public static GeneratedType ExportConfigType { get; } = new GeneratedType(identifier: TypeIdentifier.Create(
                                                                                    TypeIdentifierName.Create().AppendNamePart(nameof(Attributes.ExportConfigType)),
                                                                                    Namespace.Create().Append("RhoMicro").Append("ObjectSync").Append("Attributes")),
                                                                    source: EXPORTCONFIGTYPE_SOURCE);
        #endregion

        #region TypeExportConfiguration
        private const String TYPEEXPORTCONFIGURATION_SOURCE = @"using System;

namespace RhoMicro.ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	internal sealed class TypeExportConfigurationAttribute : Attribute
	{
		public ExportConfigType Type {
			get; set;
		}
		public String RootNamespace {
			get; set;
		}
	}
}";
        public static AttributeAnalysisUnit<TypeExportConfigurationAttribute> TypeExportConfiguration { get; } = new AttributeAnalysisUnit<TypeExportConfigurationAttribute>(TYPEEXPORTCONFIGURATION_SOURCE);
        #endregion

        #region Accessibility
        private const String ACCESSIBILITY_SOURCE =
@"namespace RhoMicro.ObjectSync.Attributes
{
	/// <summary>
	/// Enumeration for common accessibility combinations. Taken from https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.accessibility?view=roslyn-dotnet-4.3.0
	/// </summary>
	internal enum Accessibility
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
}";
        public static GeneratedType Accessibility { get; } = new GeneratedType(identifier: TypeIdentifier.Create(
                                                                                    TypeIdentifierName.Create().AppendNamePart(nameof(Attributes.Accessibility)),
                                                                                    Namespace.Create().Append("RhoMicro").Append("ObjectSync").Append("Attributes")),
                                                                    source: ACCESSIBILITY_SOURCE);
        #endregion

        #region Modifier
        private const String MODIFIER_SOURCE =
@"namespace RhoMicro.ObjectSync.Attributes
{
	internal enum Modifier
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
}";
        public static GeneratedType Modifier { get; } = new GeneratedType(identifier: TypeIdentifier.Create(
                                                                                    TypeIdentifierName.Create().AppendNamePart(nameof(Attributes.Modifier)),
                                                                                    Namespace.Create().Append("RhoMicro").Append("ObjectSync").Append("Attributes")),
                                                                    source: MODIFIER_SOURCE);
        #endregion

        #region Synchronized
        private const String SYNCHRONIZED_SOURCE = @"using System;

namespace RhoMicro.ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	internal sealed class SynchronizedAttribute : Attribute
	{
		public String PropertyName {
			get; set;
		}
		public Boolean Fast {
			get; set;
		}
		public Boolean Observable {
			get; set;
		}
		public Accessibility PropertyAccessibility { get; set; } = Attributes.Accessibility.Public;
	}
}";
        public static AttributeAnalysisUnit<SynchronizedAttribute> Synchronized { get; } = new AttributeAnalysisUnit<SynchronizedAttribute>(SYNCHRONIZED_SOURCE);
        #endregion

        #region SynchronizationTargetAttribute
        private const String SYNCHRONIZATION_TARGET_SOURCE = @"using System;

namespace RhoMicro.ObjectSync.Attributes
{	
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal sealed class SynchronizationTargetAttribute : Attribute
	{
		private String contextPropertyName = ""SynchronizationContext"";
		private Accessibility contextTypeConstructorAccessibility = Attributes.Accessibility.Public;

		public String BaseContextTypeName { get; set; } = null;

		public Accessibility ContextTypeAccessibility { get; set; } = Attributes.Accessibility.Private;
		public Boolean ContextTypeIsSealed { get; set; } = true;
		public Accessibility ContextTypeConstructorAccessibility {
			get => contextTypeConstructorAccessibility;
			set {
				if(value == Accessibility.Private)
					throw new ArgumentException($""{nameof(ContextTypeConstructorAccessibility)} cannot be {Attributes.Accessibility.Private}."");

				if(ContextTypeIsSealed && value == Accessibility.Protected)
					throw new ArgumentException($""{nameof(ContextTypeConstructorAccessibility)} cannot be {Attributes.Accessibility.Protected} while {ContextTypeIsSealed} is {true}."");

				contextTypeConstructorAccessibility = value;
			}
		}

		public String ContextPropertyName {
			get => contextPropertyName;
			set {
				if(String.IsNullOrEmpty(value))
					throw new ArgumentException($""{nameof(ContextPropertyName)} cannot be null or empty."");

				contextPropertyName = value;
			}
		}
		public Modifier ContextPropertyModifier { get; set; } = Attributes.Modifier.NotApplicable;
		public Accessibility ContextPropertyAccessibility { get; set; } = Attributes.Accessibility.Protected;
	}
}";
        public static AttributeAnalysisUnit<SynchronizationTargetAttribute> SynchronizationTarget { get; } = new AttributeAnalysisUnit<SynchronizationTargetAttribute>(SYNCHRONIZATION_TARGET_SOURCE);
        #endregion
    }
}
