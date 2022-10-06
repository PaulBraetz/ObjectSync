using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using static ObjectSync.Attributes.Attributes;

namespace ObjectSync.Generator
{
	internal sealed class SynchronizedTypeSourceFactory
	{
		#region Constants
		private const String CONTEXT_CONSTRUCTOR_PARAMETER_NAME = "instance";
		private const String CONTEXT_TYPE_SUFFIX = "SynchronizationContext";
		private const String CONTEXT_INSTANCE_PROPERTY_NAME = "Instance";
		private const String CONTEXT_EVENT_NAME = "SynchronizationStateChanged";
		private const String CONTEXT_EVENT_SUMMARY = @"/// <summary>
/// Invoked after <see cref=""" + CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME + @"""/> has changed.
/// </summary>";
		private const String CONTEXT_INSTANCE_PROPERTY_SUMMARY =
@"/// <summary>
/// The instance whose synchronized properties are to be managed.
/// </summary>";
		private const String CONTEXT_IS_SYNCHRONIZED_FIELD_NAME = "_isSynchronized";
		private const string CONTEXT_IS_SYNCHRONIZED_FIELD_SUMMARY =
@"/// <summary>
/// Logical backing field for <see cref""" + CONTEXT_EVENT_NAME + @"""/>, where 0 equals <see langword=""false""/> and 1 equals <see langword=""true""/>.
/// </summary>";

		private const String CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME = "IsSynchronized";
		private const String CONTEXT_IS_SYNCHRONIZED_PROPERTY_SUMMARY =
@"/// <summary>
/// Indicates wether the instance is synchronized.
/// </summary>";

		private const String CONTEXT_AUTHORITY_PROPERTY_NAME = "Authority";
		private const String CONTEXT_AUTHORITY_PROPERTY_SUMMARY =
@"/// <summary>
/// Provides the synchronization authority for this context.
/// </summary>";

		private const String CONTEXT_TYPE_ID_PROPERTY_NAME = "TypeId";
		private const String CONTEXT_TYPE_ID_PROPERTY_SUMMARY =
@"/// <summary>
/// Provides the type id for the instance.
/// </summary>";

		private const String CONTEXT_SOURCE_INSTANCE_ID_PROPERTY_NAME = "SourceInstanceId";
		private const String CONTEXT_SOURCE_INSTANCE_ID_PROPERTY_SUMMARY =
@"/// <summary>
/// Provides the instance id for the instance.
/// </summary>";

		private const String CONTEXT_INSTANCE_ID_PROPERTY_NAME = "InstanceId";
		private const String CONTEXT_INSTANCE_ID_PROPERTY_SUMMARY =
@"/// <summary>
/// Provides the source instance id for the instance.
/// </summary>";

		private const String CONTEXT_SYNC_ROOT_PROPERTY_NAME = "SyncRoot";
		private const String CONTEXT_SYNC_ROOT_PROPERTY_SUMMARY = @"/// <summary>
/// Sync object for synchronizing access to synchronization logic.
/// /// </summary>";

		private const String CONTEXT_INVOKE_METHOD_NAME = "Invoke";
		private const String CONTEXT_INVOKE_METHOD_SUMMARY =
@"/// <summary>
/// Invokes the methods provided in a threadsafe manner relative to the other synchronization methods.
/// This means that the synchronization state of the instance is guaranteed not to change during the invocation.
/// The method will be passed the synchronization state at the time of invocation.
/// <para>
/// Invoking any method of this instance in <paramref name=""" + CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME + @"""/> will likely cause a deadlock to occur.
/// </para>
/// </summary>
/// <param name = """ + CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME + @""">The method to invoke.</param>";
		private const String CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME = "method";

		private const String CONTEXT_DESYNCHRONIZE_METHOD_NAME = "Desynchronize";
		private const String CONTEXT_DESYNCHRONIZE_METHOD_SUMMARY = @"/// <summary>
/// Desynchronizes the instance if it is synchronized.
/// </summary>";

		private const String CONTEXT_SYNCHRONIZE_METHOD_NAME = "Synchronize";
		private const String CONTEXT_SYNCHRONIZE_METHOD_SUMMARY = @"/// <summary>
/// Synchronizes the instance if it is not synchronized.
/// </summary>";

		private const String CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_NAME = "DesynchronizeUnlocked";
		private const String CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_SUMMARY = @"/// <summary>
/// In a non-threadsafe manner, desynchronizes the instance.
/// </summary>";

		private const String CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_NAME = "SynchronizeUnlocked";
		private const String CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_SUMMARY = @"/// <summary>
/// In a non-threadsafe manner, synchronizes the instance.
/// </summary>";

		private const String CONTEXT_METHOD_TYPE_ID_LOCAL_NAME = "typeId";
		private const String CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME = "sourceInstanceId";
		private const String CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME = "instanceId";
		private const String CONTEXT_METHOD_AUTHORITY_LOCAL_NAME = "authority";
		private const String CONTEXT_METHOD_ON_REVERT_LOCAL_NAME = "onRevert";

		private const String CONTEXT_RESYNCHRONIZE_METHOD_NAME = "Resynchronize";
		private const String CONTEXT_RESYNCHRONIZE_METHOD_SUMMARY =
@"/// <summary>
/// Synchronizes the instance.
/// If it is synchronized already, it is first desynchronized.
/// </summary>";

		private const String OBSERVABLE_PROPERTY_PREFIX = "Observable";
		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";

		private const String PROPERTY_CHANGING_EVENT_METHOD_NAME = "OnPropertyChanging";
		private const String PROPERTY_CHANGED_EVENT_METHOD_NAME = "OnPropertyChanged";

		private const String TYPE_ID_DEFAULT_PROPERTY_NAME = "TypeId";
		private const String TYPE_ID_DEFAULT_PROPERTY_SUMMARY =
@"/// <summary>
/// The Id identifying this instance's type.
/// </summary/>";
		private const Boolean TYPE_ID_DEFAULT_PROPERTY_IS_STATIC = true;
		private const Boolean TYPE_ID_DEFAULT_PROPERTY_HAS_SETTER = false;

		private const String SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_NAME = "SourceInstanceId";
		private const String SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_SUMMARY =
@"/// <summary>
/// The Id identifying this instance's property data source.
/// </summary/>";
		private const Boolean SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC = false;
		private const Boolean SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_HAS_SETTER = true;

		private const String INSTANCE_ID_DEFAULT_PROPERTY_NAME = "InstanceId";
		private const String INSTANCE_ID_DEFAULT_PROPERTY_SUMMARY =
@"/// <summary>
/// The Id identifying this instance.
/// </summary/>";
		private const Boolean INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC = false;
		private const Boolean INSTANCE_ID_DEFAULT_PROPERTY_HAS_SETTER = false;

		private const String PULL_VALUE_PREFIX = "valueOf";
		#endregion

		#region Aliae
		private static TypeIdentifier TypeIdAttributeIdentifier => GeneratedAttributes.TypeId.GeneratedType.Identifier;
		private static TypeIdentifier InstanceIdAttributeIdentifier => GeneratedAttributes.InstanceId.GeneratedType.Identifier;
		private static TypeIdentifier SourceInstanceIdAttributeIdentifier => GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier;
		private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier => GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier;
		private static TypeIdentifier SynchronizationTargetAttributeIdentifier => GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier;
		private static TypeIdentifier SynchronizedAttributeIdentifier => GeneratedAttributes.Synchronized.GeneratedType.Identifier;

		private static IAttributeFactory<TypeIdAttribute> TypeIdAttributeFactory => GeneratedAttributes.TypeId.Factory;
		private static IAttributeFactory<InstanceIdAttribute> InstanceIdAttributeFactory => GeneratedAttributes.InstanceId.Factory;
		private static IAttributeFactory<SourceInstanceIdAttribute> SourceInstanceIdAttributeFactory => GeneratedAttributes.SourceInstanceId.Factory;
		private static IAttributeFactory<SynchronizationAuthorityAttribute> SynchronizationAuthorityAttributeFactory => GeneratedAttributes.SynchronizationAuthority.Factory;
		private static IAttributeFactory<SynchronizationTargetAttribute> SynchronizationTargetAttributeFactory => GeneratedAttributes.SynchronizationTarget.Factory;
		private static IAttributeFactory<SynchronizedAttribute> SynchronizedAttributeFactory => GeneratedAttributes.Synchronized.Factory;

		private static TypeIdentifier ISynchronizationAuthorityIdentifier => GeneratedSynchronizationClasses.ISynchronizationAuthority.Identifier;
		#endregion

		#region Fields
		private BaseTypeDeclarationSyntax _synchronizedTypeDeclaration;
		private SemanticModel _semanticModel;
		private Optional<GeneratedSource> _generatedSource;
		private Optional<TypeIdentifier> _synchronizedTypeIdentifier;
		private NamespaceDeclarationSyntax _namespaceDeclaration;
		private NameSyntax _namespaceName;
		private BaseTypeDeclarationSyntax _generatedTypeDeclaration;
		private MemberDeclarationSyntax _contextTypeDeclaration;
		private String _contextTypeName;
		private Optional<SyntaxToken[]> _contextTypeModifiers;
		private Optional<SynchronizationTargetAttribute> _synchronizationTargetAttribute;
		private TypeSyntax _synchronizedType;
		private TypeSyntax _contextType;
		private ExpressionSyntax _contextInstancePropertyAccess;
		private StatementSyntax[] _revertableUnsubscriptions;
		private StatementSyntax[] _revertableSubscriptions;
		private PropertyDeclarationSyntax _synchronizedTypeAuthorityProperty;
		private FieldDeclarationSyntax[] _synchronizedFields;
		private PropertyDeclarationSyntax[] _properties;
		private StatementSyntax[] _pulls;
		private StatementSyntax[] _pullAssignments;
		private PropertyDeclarationSyntax _synchronizedTypeTypeIdProperty;
		private PropertyDeclarationSyntax _synchronizedTypeInstanceIdProperty;
		private PropertyDeclarationSyntax _synchronizedTypeSourceInstanceIdProperty;
		public void Clear()
		{
			_synchronizedTypeDeclaration = default;
			_semanticModel = default;
			_synchronizedTypeIdentifier = default;
			_namespaceDeclaration = default;
			_namespaceName = default;
			_generatedTypeDeclaration = default;
			_contextTypeDeclaration = default;
			_contextTypeName = default;
			_contextTypeModifiers = default;
			_synchronizationTargetAttribute = default;
			_synchronizedType = default;
			_contextInstancePropertyAccess = default;
			_contextType = default;
			_revertableUnsubscriptions = default;
			_revertableSubscriptions = default;
			_synchronizedTypeAuthorityProperty = default;
			_properties = default;
			_pulls = default;
			_pullAssignments = default;
			_synchronizedTypeTypeIdProperty = null;
			_synchronizedTypeSourceInstanceIdProperty = null;
			_synchronizedTypeInstanceIdProperty = null;
		}

		#endregion

		#region Properties
		private TypeSyntax SynchronizedType
		{
			get
			{
				if (_synchronizedType == null)
				{
					_synchronizedType = SyntaxFactory.ParseTypeName(SynchronizedTypeIdentifier);
				}

				return _synchronizedType;
			}
		}
		private SynchronizationTargetAttribute SynchronizationTargetAttribute
		{
			get
			{
				if (!_synchronizationTargetAttribute.HasValue)
				{
					_synchronizationTargetAttribute = new Optional<SynchronizationTargetAttribute>(_synchronizedTypeDeclaration.AttributeLists
							.OfAttributeClasses(_semanticModel, SynchronizationTargetAttributeIdentifier)
							.Select(a => (success: SynchronizationTargetAttributeFactory.TryBuild(a, _semanticModel, out var attribute), attribute))
							.Where(t => t.success)
							.Select(t => t.attribute)
							.Single());
				}

				return _synchronizationTargetAttribute.Value;
			}
		}
		private TypeIdentifier SynchronizedTypeIdentifier
		{
			get
			{
				if (!_synchronizedTypeIdentifier.HasValue)
				{
					_synchronizedTypeIdentifier = TypeIdentifier.Create(_semanticModel.GetDeclaredSymbol(_synchronizedTypeDeclaration) as ITypeSymbol);
				}

				return _synchronizedTypeIdentifier.Value;
			}
		}
		private String ContextTypeName => _contextTypeName ?? (_contextTypeName = $"{SynchronizedTypeIdentifier.Name.Parts.Last()}{CONTEXT_TYPE_SUFFIX}");
		private TypeSyntax ContextType => _contextType ?? (_contextType = SyntaxFactory.ParseTypeName(ContextTypeName));
		private ExpressionSyntax ContextInstancePropertyAccess
		{
			get
			{
				if (_contextInstancePropertyAccess == null)
				{
					var identifier = SyntaxFactory.IdentifierName(CONTEXT_INSTANCE_PROPERTY_NAME);

					_contextInstancePropertyAccess = SynchronizationTargetAttribute.BaseContextTypeName == null ?
						(ExpressionSyntax)identifier :
						SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SynchronizedType, identifier));
				}

				return _contextInstancePropertyAccess;
			}
		}
		private PropertyDeclarationSyntax SynchronizedTypeAuthorityProperty
		{
			get
			{
				if (_synchronizedTypeAuthorityProperty == null)
				{
					var authorityProperties = Properties
						.Where(p => p.AttributeLists.HasAttributes(_semanticModel, SynchronizationAuthorityAttributeIdentifier))
						.ToArray();

					if (authorityProperties.Length > 1)
					{
						throw new Exception($"{SynchronizedTypeIdentifier} cannot provide multiple synchronization authorities.");
					}

					_synchronizedTypeAuthorityProperty = authorityProperties.SingleOrDefault();
				}

				return _synchronizedTypeAuthorityProperty;
			}
		}
		private PropertyDeclarationSyntax SynchronizedTypeTypeIdProperty
		{
			get
			{
				if (_synchronizedTypeTypeIdProperty == null)
				{
					var typeIdProperties = Properties
						.Where(p => p.AttributeLists.HasAttributes(_semanticModel, TypeIdAttributeIdentifier))
						.ToArray();

					if (typeIdProperties.Length > 1)
					{
						throw new Exception($"{SynchronizedTypeIdentifier} cannot provide multiple type ids.");
					}

					_synchronizedTypeTypeIdProperty = typeIdProperties.SingleOrDefault();
				}

				return _synchronizedTypeTypeIdProperty;
			}
		}
		private PropertyDeclarationSyntax SynchronizedTypeSourceInstanceIdProperty
		{
			get
			{
				if (_synchronizedTypeSourceInstanceIdProperty == null)
				{
					var sourceInstanceIdProperties = Properties
						.Where(p => p.AttributeLists.HasAttributes(_semanticModel, SourceInstanceIdAttributeIdentifier))
						.ToArray();

					if (sourceInstanceIdProperties.Length > 1)
					{
						throw new Exception($"{SynchronizedTypeIdentifier} cannot provide multiple source instance ids.");
					}

					_synchronizedTypeSourceInstanceIdProperty = sourceInstanceIdProperties.SingleOrDefault();
				}

				return _synchronizedTypeSourceInstanceIdProperty;
			}
		}
		private PropertyDeclarationSyntax SynchronizedTypeInstanceIdProperty
		{
			get
			{
				if (_synchronizedTypeInstanceIdProperty == null)
				{
					var instanceIdProperties = Properties
						.Where(p => p.AttributeLists.HasAttributes(_semanticModel, InstanceIdAttributeIdentifier))
						.ToArray();

					if (instanceIdProperties.Length > 1)
					{
						throw new Exception($"{SynchronizedTypeIdentifier} cannot provide multiple instance ids.");
					}

					_synchronizedTypeInstanceIdProperty = instanceIdProperties.SingleOrDefault();
				}

				return _synchronizedTypeInstanceIdProperty;
			}
		}
		private FieldDeclarationSyntax[] SynchronizedFields
		{
			get
			{
				return _synchronizedFields ?? (_synchronizedFields = _synchronizedTypeDeclaration.ChildNodes().OfType<FieldDeclarationSyntax>().Where(f => f.AttributeLists.HasAttributes(_semanticModel, SynchronizedAttributeIdentifier)).ToArray());
			}
		}
		private StatementSyntax[] RevertableSubscriptions
		{
			get
			{
				if (_revertableSubscriptions == null)
				{
					var requiredRevertions = new List<FieldDeclarationSyntax>();
					var expressions = SynchronizedFields.Select(f => GetRevertableSubscription(f, requiredRevertions)).ToArray();

					_revertableSubscriptions = expressions;
				}

				return _revertableSubscriptions;
			}
		}
		private StatementSyntax[] RevertableUnsubscriptions
		{
			get

			{
				if (_revertableUnsubscriptions == null)
				{
					var requiredRevertions = new List<FieldDeclarationSyntax>();
					var statements = SynchronizedFields.Select(f => GetRevertableUnsubscription(f, requiredRevertions)).ToArray();

					_revertableUnsubscriptions = statements;
				}

				return _revertableUnsubscriptions;
			}
		}
		private PropertyDeclarationSyntax[] Properties
		{
			get
			{
				return _properties ?? (_properties = _synchronizedTypeDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray());
			}
		}
		private StatementSyntax[] Pulls => _pulls ?? (_pulls = SynchronizedFields.Select(GetPull).ToArray());
		private StatementSyntax[] PullAssignments => _pullAssignments ?? (_pullAssignments = SynchronizedFields.Select(GetPullAssignment).ToArray());
		#endregion

		public SynchronizedTypeSourceFactory(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel)
		{
			_synchronizedTypeDeclaration = synchronizedType ?? throw new ArgumentNullException(nameof(synchronizedType));
			_semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
		}

		public static GeneratedSource GetSource(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel)
		{
			var source = new SynchronizedTypeSourceFactory(synchronizedType, semanticModel).GetSource();

			return source;
		}

		public GeneratedSource GetSource()
		{
			if (!_generatedSource.HasValue)
			{
				var name = SynchronizedTypeIdentifier;

				try
				{
					var source = GetNamespaceDeclaration();

					_generatedSource = new GeneratedSource(source, name);
				}
				catch (Exception ex)
				{
					var source =
$@"/*
An error occured while generating this source file for {SynchronizedTypeIdentifier}:
{ex}
*/";
					_generatedSource = new GeneratedSource(source, name);
				}
			}

			Clear();

			return _generatedSource.Value;
		}

		#region Type
		private NamespaceDeclarationSyntax GetNamespaceDeclaration()
		{
			if (_namespaceDeclaration == null)
			{
				var namespaceName = GetNamespaceName();
				var generatedTypeDeclaration = GetGeneratedTypeDeclaration();

				_namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(namespaceName)
						.AddMembers(generatedTypeDeclaration);
			}

			return _namespaceDeclaration;
		}
		private NameSyntax GetNamespaceName()
		{
			if (_namespaceName == null)
			{
				if (TryGetNamespace(_synchronizedTypeDeclaration, out var namespaceDeclaration))
				{
					_namespaceName = namespaceDeclaration.Name;
				}
				else
				{
					var synchronizedTypeDeclarationName = SynchronizedTypeIdentifier;

					throw new Exception($"{synchronizedTypeDeclarationName} was not declared in a namespace.");
				}
			}

			return _namespaceName;
		}
		private MemberDeclarationSyntax[] GetGeneratedTypeMembers()
		{
			var members = new MemberDeclarationSyntax[]
				{
					GetContextDeclaration()
				}.Concat(GetIdDeclarations())
				.Where(m => m != null)
				.ToArray();

			return members;
		}
		private BaseTypeDeclarationSyntax GetGeneratedTypeDeclaration()
		{
			if (_generatedTypeDeclaration == null)
			{
				var synchronizedTypeDeclarationName = SynchronizedTypeIdentifier;
				var generatedTypeMembers = GetGeneratedTypeMembers();

				_generatedTypeDeclaration = SyntaxFactory.TypeDeclaration(SyntaxKind.ClassDeclaration, synchronizedTypeDeclarationName)
					.WithModifiers(_synchronizedTypeDeclaration.Modifiers)
					.WithMembers(new SyntaxList<MemberDeclarationSyntax>(generatedTypeMembers));
			}

			return _generatedTypeDeclaration;
		}
		#endregion

		#region Context
		private SyntaxToken[] GetContextTypeModifiers()
		{
			if (!_contextTypeModifiers.HasValue)
			{
				IEnumerable<SyntaxKind> kinds = SynchronizationTargetAttribute.ContextTypeAccessibility.AsSyntax();

				if (SynchronizationTargetAttribute.ContextTypeIsSealed)
				{
					kinds = kinds.Append(SyntaxKind.SealedKeyword);
				}

				var tokens = kinds.Select(SyntaxFactory.Token).ToArray();

				_contextTypeModifiers = new Optional<SyntaxToken[]>(tokens);
			}

			return _contextTypeModifiers.Value;
		}
		private MemberDeclarationSyntax GetContextDeclaration()
		{
			if (_contextTypeDeclaration == null)
			{
				var kind = _synchronizedTypeDeclaration.Kind();
				var name = ContextTypeName;
				var modifiers = GetContextTypeModifiers();
				var members = GetContextMembers();

				var contextTypeDeclaration = SyntaxFactory.TypeDeclaration(kind, name)
					.AddModifiers(modifiers)
					.AddMembers(members);

				if (SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					contextTypeDeclaration = contextTypeDeclaration.WithBaseList(
						SyntaxFactory.BaseList()
						.AddTypes(
							SyntaxFactory.SimpleBaseType(
								SyntaxFactory.ParseTypeName(SynchronizationTargetAttribute.BaseContextTypeName))));
				}

				_contextTypeDeclaration = contextTypeDeclaration;
			}

			return _contextTypeDeclaration;
		}
		private MemberDeclarationSyntax[] GetContextMembers()
		{
			var members = new MemberDeclarationSyntax[]
			{
				//GetContextEvent(),
				//GetContextIsSynchronizedField(),
				//GetContextIsSynchronizedProperty(),
				//GetContextInstanceProperty(),
				//GetContextSyncRootProperty(),
				//GetContextConstructor(),
				//GetContextInvokeMethod(),
				//GetContextDesynchronizeMethod(),
				//GetContextDesynchronizeUnlockedMethod(),
				//GetContextSynchronizeMethod(),
				//GetContextSynchronizeUnlockedMethod(),
				//GetContextResynchronizeMethod(),
				GetContextAuthorityProperty(),
				GetContextTypeIdProperty(),
				GetContextSourceInstanceIdProperty(),
				GetContextInstanceIdProperty(),
				
				//GetContextDesynchronizeInvokeSynchronizeMethod()
			}
			.Where(m => m != null)
			.ToArray();

			return members;
		}

		private ConstructorDeclarationSyntax GetContextConstructor()
		{
			var synchronizedTypeName = SynchronizedTypeIdentifier;
			var contextTypeName = ContextTypeName;

			var parameterName = CONTEXT_CONSTRUCTOR_PARAMETER_NAME;

			var constructor = SyntaxFactory.ConstructorDeclaration(contextTypeName)
				.AddModifiers(SynchronizationTargetAttribute.ContextTypeConstructorAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
				.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)).WithType(SynchronizedType));

			constructor = SynchronizationTargetAttribute.BaseContextTypeName == null
				? constructor.AddBodyStatements(
					SyntaxFactory.ParseStatement(text: $"this.{CONTEXT_INSTANCE_PROPERTY_NAME} = {parameterName};"))
				: constructor.WithInitializer(
					SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
						.AddArgumentListArguments(
								SyntaxFactory.Argument(
									SyntaxFactory.ParseExpression(parameterName))))
				.AddBodyStatements();

			return constructor;
		}
		private PropertyDeclarationSyntax GetContextInstanceProperty()
		{
			PropertyDeclarationSyntax property = null;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
					.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

				if (SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.Private &&
					SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.NotApplicable)
				{
					setter = setter.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
				}

				property = SyntaxFactory.PropertyDeclaration(SynchronizedType, CONTEXT_INSTANCE_PROPERTY_NAME)
					.WithAccessorList(
						SyntaxFactory.AccessorList()
						.AddAccessors(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
							.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
						.AddAccessors(setter))
					.AddModifiers(SynchronizationTargetAttribute.ContextPropertyAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
					.WithLeadingTrivia(CONTEXT_INSTANCE_PROPERTY_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}

			return property;
		}
		private FieldDeclarationSyntax GetContextIsSynchronizedField()
		{
			FieldDeclarationSyntax field;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				field = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Int32>())))
					.AddDeclarationVariables(SyntaxFactory.VariableDeclarator(CONTEXT_IS_SYNCHRONIZED_FIELD_NAME))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
					.WithLeadingTrivia(CONTEXT_IS_SYNCHRONIZED_FIELD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				field = null;
			}

			return field;
		}
		private PropertyDeclarationSyntax GetContextIsSynchronizedProperty()
		{
			PropertyDeclarationSyntax property;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Boolean>()),
						CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME)
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement("var valueInt = value?1:0;"),
							SyntaxFactory.ParseStatement($"var requiredValueInt = this.{CONTEXT_IS_SYNCHRONIZED_FIELD_NAME} == 1?0:1;"),
							SyntaxFactory.ParseStatement(
$@"if(System.Threading.Interlocked.CompareExchange(ref {CONTEXT_IS_SYNCHRONIZED_FIELD_NAME}, valueInt, requiredValueInt) == requiredValueInt)
{{
	this.{CONTEXT_IS_SYNCHRONIZED_FIELD_NAME} = valueInt;
	this.{CONTEXT_EVENT_NAME}?.Invoke(this, value);
}}")),
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
$@"return this.{CONTEXT_IS_SYNCHRONIZED_FIELD_NAME} == 1;")))
					.WithLeadingTrivia(CONTEXT_IS_SYNCHRONIZED_PROPERTY_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				property = null;
			}

			return property;
		}
		private EventFieldDeclarationSyntax GetContextEvent()
		{
			EventFieldDeclarationSyntax @event;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				@event = SyntaxFactory.EventFieldDeclaration(
					SyntaxFactory.VariableDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<EventHandler<Boolean>>())))
					.AddDeclarationVariables(
						SyntaxFactory.VariableDeclarator(CONTEXT_EVENT_NAME))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.WithLeadingTrivia(CONTEXT_EVENT_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				@event = null;
			}

			return @event;
		}
		private PropertyDeclarationSyntax GetContextSyncRootProperty()
		{
			PropertyDeclarationSyntax property;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Object>()),
						CONTEXT_SYNC_ROOT_PROPERTY_NAME)
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration))
					.AddModifiers(
						SyntaxFactory.Token(
							SynchronizationTargetAttribute.ContextTypeIsSealed ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword))
					.WithLeadingTrivia(CONTEXT_SYNC_ROOT_PROPERTY_SUMMARY.Split('\n').Select(SyntaxFactory.Comment))
					.WithInitializer(
						SyntaxFactory.EqualsValueClause(
							SyntaxFactory.ParseExpression($"new {TypeIdentifier.Create<Object>()}()")));
			}
			else
			{
				property = null;
			}

			return property;
		}
		private MethodDeclarationSyntax GetContextInvokeMethod()
		{
			MethodDeclarationSyntax method;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_INVOKE_METHOD_NAME)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME))
					.WithType(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action<Boolean>>())))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if({CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME} != null)
{{
	lock({CONTEXT_SYNC_ROOT_PROPERTY_NAME})
	{{
		{CONTEXT_INVOKE_METHOD_METHOD_PARAMETER_NAME}.Invoke({CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME});
	}}
}}"))
				.WithLeadingTrivia(CONTEXT_INVOKE_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		private MethodDeclarationSyntax GetContextDesynchronizeMethod()
		{
			MethodDeclarationSyntax method;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_DESYNCHRONIZE_METHOD_NAME)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if (this.{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME})
{{
	lock ({CONTEXT_SYNC_ROOT_PROPERTY_NAME})
	{{
		if (this.{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME})
		{{
			var authority = {GetAuthorityPropertyAccess(true)};
			if (authority != null)
			{{
				var typeId = {GetTypeIdPropertyAccess(true)};
				var sourceInstanceId = {GetSourceInstanceIdPropertyAccess(true)};
				var instanceId = {GetInstanceIdPropertyAccess(true)};

				this.{CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_NAME}(typeId, sourceInstanceId, instanceId, null);
			}}

			{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME} = false;
		}}
	}}
}}"))
				.WithLeadingTrivia(CONTEXT_DESYNCHRONIZE_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		private MethodDeclarationSyntax GetContextDesynchronizeUnlockedMethod()
		{
			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_NAME)
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_TYPE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_AUTHORITY_LOCAL_NAME))
					.WithType(SynchronizedTypeAuthorityProperty.Type),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_ON_REVERT_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())))
				.AddModifiers(
					SyntaxFactory.Token(
						SyntaxKind.ProtectedKeyword),
					SyntaxFactory.Token(
						SynchronizationTargetAttribute.BaseContextTypeName == null ?
						SyntaxKind.VirtualKeyword :
						SyntaxKind.OverrideKeyword));

			method = method
				.AddBodyStatements(RevertableUnsubscriptions)
				.WithLeadingTrivia(CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));

			if (SynchronizationTargetAttribute.BaseContextTypeName != null)
			{
				method = method
					.AddBodyStatements(
						SyntaxFactory.ParseStatement($"base.{CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_NAME}(" +
													 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}: () => this.{CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_NAME}(" +
													 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}: null));"));
			}

			return method;
		}
		private MethodDeclarationSyntax GetContextSynchronizeMethod()
		{
			MethodDeclarationSyntax method;

			if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_SYNCHRONIZE_METHOD_NAME)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if (!this.{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME})
{{
	lock ({CONTEXT_SYNC_ROOT_PROPERTY_NAME})
	{{
		if (!this.{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME})
		{{
			var authority = {{authorityAccess}};
			if (authority != null)
			{{
				var typeId = {{typeIdAccess}};
				var sourceInstanceId = {{sourceInstanceIdAccess}};
				var instanceId = {{instanceIdAccess}};

				this.{CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_NAME}(typeId, sourceInstanceId, instanceId, null);
			}}

			{CONTEXT_IS_SYNCHRONIZED_PROPERTY_NAME} = true;
		}}
	}}
}}"))
				.WithLeadingTrivia(CONTEXT_SYNCHRONIZE_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		private MethodDeclarationSyntax GetContextSynchronizeUnlockedMethod()
		{
			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_NAME)
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_TYPE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_AUTHORITY_LOCAL_NAME))
					.WithType(SynchronizedTypeAuthorityProperty.Type),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(CONTEXT_METHOD_ON_REVERT_LOCAL_NAME))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())))
				.AddModifiers(
					SyntaxFactory.Token(
						SyntaxKind.ProtectedKeyword),
					SyntaxFactory.Token(
						SynchronizationTargetAttribute.BaseContextTypeName == null ?
						SyntaxKind.VirtualKeyword :
						SyntaxKind.OverrideKeyword));

			method = method
				.AddBodyStatements(RevertableSubscriptions)
				.WithLeadingTrivia(CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));

			if (SynchronizationTargetAttribute.BaseContextTypeName != null)
			{
				method = method
					.AddBodyStatements(
						SyntaxFactory.ParseStatement($"base.{CONTEXT_SYNCHRONIZE_UNLOCKED_METHOD_NAME}(" +
													 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}: () => this.{CONTEXT_DESYNCHRONIZE_UNLOCKED_METHOD_NAME}(" +
													 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME}, " +
													 $"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}: null));"));
			}

			return method;
		}
		private MethodDeclarationSyntax GetContextResynchronizeMethod()
		{
			//TODO: continue with Resynchronization / UnlockedResynchronization

			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);
			var unsubscriptions = RevertableUnsubscriptions;
			var subscriptions = RevertableSubscriptions;
			var pulls = Pulls;
			var pullAssignments = PullAssignments;

			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					CONTEXT_RESYNCHRONIZE_METHOD_NAME)
				.WithLeadingTrivia(CONTEXT_RESYNCHRONIZE_METHOD_SUMMARY.Split('\n').Select(SyntaxFactory.Comment))
				.AddBodyStatements(
					SyntaxFactory.LockStatement(
						SyntaxFactory.ParseExpression($"this.{CONTEXT_SYNC_ROOT_PROPERTY_NAME}"),
						SyntaxFactory.EmptyStatement()));

			/*
			var authority = {authorityAccess};
			if (authority != null)
			{{
				var typeId = {typeIdAccess};
				var sourceInstanceId = {sourceInstanceIdAccess};
				var instanceId = {instanceIdAccess};

				if (_isSynchronized)
				{{
					{unsubscriptions}

					{GetIsSynchronizedSet(false, false)}
				}}

				{subscriptions}

				{pulls}

				{pullAssignments}
			
				{GetIsSynchronizedSet(true, false)}
			}}
			else {GetIsSynchronizedSet(true, true)}
			 */

			return method;
		}
		private PropertyDeclarationSyntax GetContextAuthorityProperty()
		{
			PropertyDeclarationSyntax property;

			if (SynchronizedTypeAuthorityProperty != null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						ISynchronizationAuthorityIdentifier.AsSyntax(),
						CONTEXT_AUTHORITY_PROPERTY_NAME)
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(
							SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
								$"return {CONTEXT_INSTANCE_PROPERTY_NAME}.{SynchronizedTypeAuthorityProperty.Identifier};")))
					.AddModifiers(
						SyntaxFactory.Token(
							SynchronizationTargetAttribute.ContextTypeIsSealed &&
							SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword));

				if (SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					property = property
						.AddModifiers(
							SyntaxFactory.Token(
								SyntaxKind.OverrideKeyword));
				}

				property = property.WithLeadingTrivia(CONTEXT_AUTHORITY_PROPERTY_SUMMARY.Split('\n').Select(SyntaxFactory.Comment));
			}
			else if (SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				throw new Exception($"Either {SynchronizedTypeIdentifier} or one of its synchronized base classes must provide a property annotated with {SynchronizationAuthorityAttributeIdentifier}.");
			}
			else
			{
				property = null;
			}

			return property;
		}
		private PropertyDeclarationSyntax GetContextTypeIdProperty()
		{
			var property = SyntaxFactory.PropertyDeclaration(
					ISynchronizationAuthorityIdentifier.AsSyntax(),
					CONTEXT_TYPE_ID_PROPERTY_NAME);

			if (SynchronizedTypeTypeIdProperty != null)
			{
				property = property
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(
							SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
								$"return {CONTEXT_INSTANCE_PROPERTY_NAME}.{SynchronizedTypeTypeIdProperty.Identifier};")))
					.AddModifiers(
						SyntaxFactory.Token(
							SynchronizationTargetAttribute.ContextTypeIsSealed &&
							SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword));

				if (SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					property = property
						.AddModifiers(
							SyntaxFactory.Token(
								SyntaxKind.OverrideKeyword));
				}
			}
			else
			{
				property = null;
			}

			return property;
		}
		private PropertyDeclarationSyntax GetContextSourceInstanceIdProperty()
		{
			return null;
		}
		private PropertyDeclarationSyntax GetContextInstanceIdProperty()
		{
			return null;
		}

		private StatementSyntax GetRevertableUnsubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
		{
			requiredRevertions.Add(field);

			var statement = SyntaxFactory.TryStatement()
				.AddBlockStatements(
					GetUnsubscription(field))
				.AddCatches(
					SyntaxFactory.CatchClause()
						.AddBlockStatements(
							requiredRevertions.Select(f => GetSubscription(f))
							.Append(SyntaxFactory.ParseStatement($"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}?.Invoke();"))
							.Append(SyntaxFactory.ThrowStatement())
							.ToArray()));

			return statement;
		}
		private StatementSyntax GetUnsubscription(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);

			var statement = SyntaxFactory.ParseStatement($"{CONTEXT_METHOD_AUTHORITY_LOCAL_NAME}.Unsubscribe(" +
														 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, \"" +
														 $"{propertyName}\", " +
														 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
														 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME});");

			return statement;
		}

		private StatementSyntax GetRevertableSubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
		{
			requiredRevertions.Add(field);

			var subscription = GetSubscription(field);
			var revertion = String.Join("\n", requiredRevertions.Select(f => GetUnsubscription(f)));

			var statement = SyntaxFactory.TryStatement()
				.AddBlockStatements(
					GetSubscription(field))
				.AddCatches(
					SyntaxFactory.CatchClause()
						.AddBlockStatements(
							requiredRevertions.Select(f => GetUnsubscription(f))
							.Append(SyntaxFactory.ParseStatement($"{CONTEXT_METHOD_ON_REVERT_LOCAL_NAME}?.Invoke();"))
							.Append(SyntaxFactory.ThrowStatement())
							.ToArray()));

			return statement;
		}
		private StatementSyntax GetSubscription(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setBlock = GetSetBlock(field, fromWithinContext: true);

			var statement = SyntaxFactory.ParseStatement($"{CONTEXT_METHOD_AUTHORITY_LOCAL_NAME}.Subscribe<" +
														 $"{fieldType}>(" +
														 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, \"" +
														 $"{propertyName}\", " +
														 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
														 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME}, (value) => {{" +
														 $"{setBlock}}});");

			return statement;
		}

		private StatementSyntax GetPull(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);

			var statement = SyntaxFactory.ParseStatement($"var {PULL_VALUE_PREFIX}{propertyName} = " +
														 $"{CONTEXT_METHOD_AUTHORITY_LOCAL_NAME}.Pull<" +
														 $"{fieldType}>(" +
														 $"{CONTEXT_METHOD_TYPE_ID_LOCAL_NAME}, \"" +
														 $"{propertyName}\", " +
														 $"{CONTEXT_METHOD_SOURCE_INSTANCE_ID_LOCAL_NAME}, " +
														 $"{CONTEXT_METHOD_INSTANCE_ID_LOCAL_NAME});");

			return statement;
		}
		private StatementSyntax GetPullAssignment(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);
			var fieldName = GetFieldName(field);

			var statement = SyntaxFactory.ParseStatement($"{CONTEXT_INSTANCE_PROPERTY_NAME}." +
														 $"{fieldName} = " +
														 $"{PULL_VALUE_PREFIX}" +
														 $"{propertyName};");

			return statement;
		}
		#endregion

		#region Ids
		private PropertyDeclarationSyntax[] GetIdDeclarations()
		{
			var declarations = new[]
			{
				GetTypeIdPropertyDeclaration(),
				GetSourceInstanceIdPropertyDeclaration(),
				GetInstanceIdPropertyDeclaration()
			}
			.Where(d => d != null)
			.ToArray();

			return declarations;
		}

		private Boolean TryGetIdProperty(TypeIdentifier idAttributeIdentifier, out PropertyDeclarationSyntax idProperty)
		{
			var properties = Properties.Where(p => p.AttributeLists.HasAttributes(_semanticModel, idAttributeIdentifier)).ToArray();
			ThrowIfMultiple(properties, "properties", idAttributeIdentifier);

			idProperty = properties.SingleOrDefault();

			return idProperty != null;
		}
		private String GetIdPropertyName(TypeIdentifier idAttributeIdentifier, String fallbackName)
		{
			var name = TryGetIdProperty(idAttributeIdentifier, out var property) ?
				property.Identifier.Text :
				fallbackName;

			return name;
		}
		private PropertyDeclarationSyntax GetIdPropertyDeclaration(TypeIdentifier idAttributeIdentifier, String fallbackName, String fallbackSummary, Boolean defaultIsStatic, Boolean defaultHasSetter)
		{
			PropertyDeclarationSyntax property;

			if (!TryGetIdProperty(idAttributeIdentifier, out _) && SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						TypeIdentifier.Create<String>().AsSyntax(),
						fallbackName)
					.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.PrivateKeyword))
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(
							SyntaxKind.SetAccessorDeclaration)
						.WithSemicolonToken(
							SyntaxFactory.Token(
								SyntaxKind.SemicolonToken)))
					.WithInitializer(
						SyntaxFactory.EqualsValueClause(
							SyntaxFactory.ParseExpression("System.Guid.NewGuid().ToString()"))
						.WithTrailingTrivia(
							SyntaxFactory.Comment("\n")));

				if (defaultHasSetter)
				{
					property = property
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(
								SyntaxKind.GetAccessorDeclaration)
							.WithSemicolonToken(
								SyntaxFactory.Token(
									SyntaxKind.SemicolonToken)));
				}
				if (defaultIsStatic)
				{
					property = property
						.AddModifiers(
							SyntaxFactory.Token(
								SyntaxKind.StaticKeyword));
				}
				property = property.WithLeadingTrivia(fallbackSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				property = null;
			};

			return property;
		}
		private String GetIdPropertyAccess(TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic, Boolean accessingInContext)
		{
			var propertyName = GetIdPropertyName(idAttributeIdentifier, fallbackName);

			_ = TryGetIdProperty(idAttributeIdentifier, out var idProperty);
			var isStatic = idProperty?.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)) ?? defaultIsStatic;

			String access = null;

			if (isStatic)
			{
				access = $"{SynchronizedTypeIdentifier}.{propertyName}";
			}
			else if (accessingInContext)
			{
				access = $"{CONTEXT_INSTANCE_PROPERTY_NAME}.{propertyName}";
			}
			else
			{
				access = propertyName;
			}

			return access;
		}

		private Boolean TryGetTypeIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(TypeIdAttributeIdentifier, out idProperty);
		}
		private PropertyDeclarationSyntax GetTypeIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_PROPERTY_NAME, TYPE_ID_DEFAULT_PROPERTY_SUMMARY, TYPE_ID_DEFAULT_PROPERTY_IS_STATIC, TYPE_ID_DEFAULT_PROPERTY_HAS_SETTER);
		}
		private String GetTypeIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_PROPERTY_NAME, TYPE_ID_DEFAULT_PROPERTY_IS_STATIC, accessingInContext);
		}

		private Boolean TryGetSourceInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(SourceInstanceIdAttributeIdentifier, out idProperty);
		}
		private PropertyDeclarationSyntax GetSourceInstanceIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_NAME, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_SUMMARY, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_HAS_SETTER);
		}
		private String GetSourceInstanceIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_NAME, SOURCE_INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC, accessingInContext);
		}

		private Boolean TryGetInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(InstanceIdAttributeIdentifier, out idProperty);
		}
		private PropertyDeclarationSyntax GetInstanceIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_PROPERTY_NAME, INSTANCE_ID_DEFAULT_PROPERTY_SUMMARY, INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC, INSTANCE_ID_DEFAULT_PROPERTY_HAS_SETTER);
		}
		private String GetInstanceIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_PROPERTY_NAME, INSTANCE_ID_DEFAULT_PROPERTY_IS_STATIC, accessingInContext);
		}
		#endregion

		#region Misc
		private String GetAuthorityPropertyAccess(Boolean accessingInContext)
		{
			var authorityName = SynchronizedTypeAuthorityProperty.Identifier.Text;

			var access = accessingInContext ?
				$"{CONTEXT_INSTANCE_PROPERTY_NAME}.{authorityName}" :
				authorityName;

			return access;
		}

		private String GetSetBlock(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_PROPERTY_NAME}." :
				"this.";
			var fieldName = GetFieldName(field);
			var propertyChangedCall = GetPropertyChangedCall(field, fromWithinContext);

			var del =
$@"{propertyChangingCall}
{instance}{fieldName} = value;{propertyChangedCall}";

			return del;
		}
		private String GetPropertyChangingCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_PROPERTY_NAME}." :
				String.Empty;
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, _semanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}{PROPERTY_CHANGING_EVENT_METHOD_NAME}(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_PROPERTY_NAME}." :
				String.Empty;
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, _semanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}{PROPERTY_CHANGED_EVENT_METHOD_NAME}(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private TypeIdentifier GetFieldType(FieldDeclarationSyntax field)
		{
			var type = field.Declaration.Type;
			var symbol = _semanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? _semanticModel.GetTypeInfo(type).Type;

			var identifier = TypeIdentifier.Create(symbol);

			return identifier;
		}
		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			var attributeInstance = field.AttributeLists.SelectMany(al => al.Attributes)
				.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, _semanticModel, out var instance), instance))
				.FirstOrDefault(t => t.success).instance;

			var propertyName = attributeInstance?.PropertyName;
			var isObservable = attributeInstance?.Observable ?? false;

			if (String.IsNullOrEmpty(propertyName))
			{
				var fieldName = GetFieldName(field);

				if (fieldName[0] == '_' || fieldName[0] == Char.ToLowerInvariant(fieldName[0]))
				{
					var sanitizedFieldName = Regex.Replace(fieldName, @"^_*", String.Empty);
					propertyName = String.Concat(Char.ToUpperInvariant(sanitizedFieldName[0]), sanitizedFieldName.Substring(1, sanitizedFieldName.Length - 1));
				}
				else if (isObservable)
				{
					propertyName = getPrefixedName(OBSERVABLE_PROPERTY_PREFIX);
				}
				else
				{
					propertyName = getPrefixedName(SYNCHRONIZED_PROPERTY_PREFIX);
				}

				String getPrefixedName(String prefix)
				{
					return String.Concat(prefix, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
				}
			}

			return propertyName;
		}
		private Boolean TryGetNamespace(SyntaxNode node, out BaseNamespaceDeclarationSyntax namespaceDeclaration)
		{
			while (node.Parent != null && !(node is BaseNamespaceDeclarationSyntax))
			{
				node = node.Parent;
			}

			namespaceDeclaration = node == null ?
				null :
				node as BaseNamespaceDeclarationSyntax;

			return namespaceDeclaration != null;
		}

		private void ThrowIfMultiple<T>(T[] items, String declarationType, TypeIdentifier attribute)
		{
			if (items.Length > 1)
			{
				throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {SynchronizedTypeIdentifier}.");
			}
		}
		#endregion
	}
}
