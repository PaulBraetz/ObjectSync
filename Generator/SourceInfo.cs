using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Reflection;

namespace ObjectSync.Generator
{
	internal sealed class SourceInfo
	{
		internal sealed class DeclaredInfo
		{
			private readonly SourceInfo _parent;

			#region Properties
			public BaseTypeDeclarationSyntax Type { get; }
			public SemanticModel SemanticModel { get; }

			private Optional<TypeIdentifier> _typeIdentifier;
			public TypeIdentifier TypeIdentifier
			{
				get
				{
					if (!_typeIdentifier.HasValue)
					{
						_typeIdentifier = TypeIdentifier.Create(SemanticModel.GetDeclaredSymbol(Type) as ITypeSymbol);
					}

					return _typeIdentifier.Value;
				}
			}

			private SynchronizationTargetAttribute _synchronizationTargetAttribute;
			public SynchronizationTargetAttribute SynchronizationTargetAttribute
			{
				get
				{
					if (_synchronizationTargetAttribute == null)
					{
						_synchronizationTargetAttribute = Type.AttributeLists
								.OfAttributeClasses(SemanticModel, _parent.SynchronizationTargetAttributeIdentifier)
								.Select(a => (success: _parent.SynchronizationTargetAttributeFactory.TryBuild(a, SemanticModel, out var attribute), attribute))
								.Where(t => t.success)
								.Select(t => t.attribute)
								.Single();
					}

					return _synchronizationTargetAttribute;
				}
			}

			private TypeSyntax _typeSyntax;
			public TypeSyntax TypeSyntax
			{
				get
				{
					if (_typeSyntax == null)
					{
						_typeSyntax = SyntaxFactory.ParseTypeName(TypeIdentifier);
					}

					return _typeSyntax;
				}
			}

			private FieldDeclarationSyntax[] _synchronizedFields;
			public FieldDeclarationSyntax[] SynchronizedFields => _synchronizedFields ?? (_synchronizedFields = Type.ChildNodes().OfType<FieldDeclarationSyntax>().Where(f => f.AttributeLists.HasAttributes(SemanticModel, _parent.SynchronizedAttributeIdentifier)).ToArray());

			private PropertyDeclarationSyntax[] _properties;
			public PropertyDeclarationSyntax[] Properties => _properties ?? (_properties = Type.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray());

			private PropertyDeclarationSyntax _typeAuthority;
			public PropertyDeclarationSyntax Authority
			{
				get
				{
					if (_typeAuthority == null)
					{
						var authorityProperties = Properties
							.Where(p => p.AttributeLists.HasAttributes(SemanticModel, _parent.SynchronizationAuthorityAttributeIdentifier))
							.ToArray();

						if (authorityProperties.Length > 1)
						{
							throw new Exception($"{TypeIdentifier} cannot provide multiple synchronization authorities.");
						}

						_typeAuthority = authorityProperties.SingleOrDefault();
					}

					return _typeAuthority;
				}
			}

			private PropertyDeclarationSyntax _typeId;
			public PropertyDeclarationSyntax TypeId
			{
				get
				{
					if (_typeId == null)
					{
						var typeIdProperties = Properties
							.Where(p => p.AttributeLists.HasAttributes(SemanticModel, _parent.TypeIdAttributeIdentifier))
							.ToArray();

						if (typeIdProperties.Length > 1)
						{
							throw new Exception($"{TypeIdentifier} cannot provide multiple type ids.");
						}

						_typeId = typeIdProperties.SingleOrDefault();
					}

					return _typeId;
				}
			}

			private PropertyDeclarationSyntax _typeSourceInstanceId;
			public PropertyDeclarationSyntax SourceInstanceId
			{
				get
				{
					if (_typeSourceInstanceId == null)
					{
						var sourceInstanceIdProperties = Properties
							.Where(p => p.AttributeLists.HasAttributes(SemanticModel, _parent.SourceInstanceIdAttributeIdentifier))
							.ToArray();

						if (sourceInstanceIdProperties.Length > 1)
						{
							throw new Exception($"{TypeIdentifier} cannot provide multiple source instance ids.");
						}

						_typeSourceInstanceId = sourceInstanceIdProperties.SingleOrDefault();
					}

					return _typeSourceInstanceId;
				}
			}

			private PropertyDeclarationSyntax _instanceId;
			public PropertyDeclarationSyntax InstanceId
			{
				get
				{
					if (_instanceId == null)
					{
						var instanceIdProperties = Properties
							.Where(p => p.AttributeLists.HasAttributes(SemanticModel, _parent.InstanceIdAttributeIdentifier))
							.ToArray();

						if (instanceIdProperties.Length > 1)
						{
							throw new Exception($"{TypeIdentifier} cannot provide multiple instance ids.");
						}

						_instanceId = instanceIdProperties.SingleOrDefault();
					}

					return _instanceId;
				}
			}
			#endregion

			public DeclaredInfo(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel, SourceInfo parent)
			{
				this.Type = synchronizedType ?? throw new ArgumentNullException(nameof(synchronizedType));
				this.SemanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
				this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
			}
		}
		internal sealed class ContextInfo
		{
			private readonly SourceInfo _parent;
			private readonly SynchronizedTypeSourceFactory _factory;

			#region Constants
			public String ConstructorParameterName => "instance";
			public String TYPE_SUFFIX => "SynchronizationContext";
			public String InstancePropertyName => "Instance";
			public String EventName => "SynchronizationStateChanged";
			public String EventSummary => @"/// <summary>
/// Invoked after <see cref=""" + IsSynchronizedPropertyName + @"""/> has changed.
/// </summary>";
			public String InstancePropertySummary =>
	@"/// <summary>
/// The instance whose synchronized properties are to be managed.
/// </summary>";
			public String IsSynchronizedFieldName => "_isSynchronized";
			public string IsSynchronizedFieldSummary =>
	@"/// <summary>
/// Logical backing field for <see cref""" + EventName + @"""/>, where 0 equals <see langword=""false""/> and 1 equals <see langword=""true""/>.
/// </summary>";

			public String IsSynchronizedPropertyName => "IsSynchronized";
			public String IsSynchronizedPropertySummary =>
	@"/// <summary>
/// Indicates wether the instance is synchronized.
/// </summary>";

			public String AuthorityPropertyName => "Authority";
			public String AuthorityPropertySummary =>
	@"/// <summary>
/// Provides the synchronization authority for this context.
/// </summary>";

			public String TypeIdPropertyName => "TypeId";
			public String TypeIdPropertySummary =>
	@"/// <summary>
/// Provides the type id for the instance.
/// </summary>";

			public String SourceIdPropertyName => "SourceInstanceId";
			public String SOURCE_INSTANCE_ID_PROPERTY_SUMMARY =>
	@"/// <summary>
/// Provides the instance id for the instance.
/// </summary>";

			public String INSTANCE_ID_PROPERTY_NAME => "InstanceId";
			public String INSTANCE_ID_PROPERTY_SUMMARY =>
	@"/// <summary>
/// Provides the source instance id for the instance.
/// </summary>";

			public String SyncRootPropertyName => "SyncRoot";
			public String SyncRootSummary => @"/// <summary>
/// Sync object for synchronizing access to synchronization logic.
/// /// </summary>";

			public String InvokeMethodName => "Invoke";
			public String InvokeMethodSummary =>
	@"/// <summary>
/// Invokes the methods provided in a threadsafe manner relative to the other synchronization methods.
/// This means that the synchronization state of the instance is guaranteed not to change during the invocation.
/// The method will be passed the synchronization state at the time of invocation.
/// <para>
/// Invoking any method of this instance in <paramref name=""" + InvokeMethodMethodParameterName + @"""/> will likely cause a deadlock to occur.
/// </para>
/// </summary>
/// <param name = """ + InvokeMethodMethodParameterName + @""">The method to invoke.</param>";
			public String InvokeMethodMethodParameterName => "method";

			public String DesynchronizeMethodName => "Desynchronize";
			public String DesynchronizeMethodSummary => @"/// <summary>
/// Desynchronizes the instance if it is synchronized.
/// </summary>";

			public String SynchronizeMethodName => "Synchronize";
			public String SynchronizeMethodSummary => @"/// <summary>
/// Synchronizes the instance if it is not synchronized.
/// </summary>";

			public String DesynchronizeUnlockedMethodName => "DesynchronizeUnlocked";
			public String DesynchronizeUnlockedMethodSummary => @"/// <summary>
/// In a non-threadsafe manner, desynchronizes the instance.
/// </summary>";

			public String SynchronizeUnlockedMethodName => "SynchronizeUnlocked";
			public String SynchronizeUnlockedMethodSummary => @"/// <summary>
/// In a non-threadsafe manner, synchronizes the instance.
/// </summary>";

			public String TypeIdLocalName => "typeId";
			public String SourceInstanceIdLocalName => "sourceInstanceId";
			public String InstanceIdLocalName => "instanceId";
			public String LocalAuthorityName => "authority";
			public String OnRevertLocalName => "onRevert";

			public String ResynchronizeMethodName => "Resynchronize";
			public String ResynchronizeMethodSummary =>
	@"/// <summary>
/// Synchronizes the instance.
/// If it is synchronized already, it is first desynchronized.
/// </summary>";
			public String PullValuePrefix => "valueOf";
			#endregion

			#region Properties
			private String _typeName;
			public String TypeName => _typeName ?? (_typeName = $"{_parent.Declared.TypeIdentifier.Name.Parts.Last()}{TYPE_SUFFIX}");

			private TypeSyntax _typeSyntax;
			public TypeSyntax TypeSyntax => _typeSyntax ?? (_typeSyntax = SyntaxFactory.ParseTypeName(TypeName));

			private ExpressionSyntax _instancePropertyAccess;
			public ExpressionSyntax InstancePropertyAccess
			{
				get
				{
					if (_instancePropertyAccess == null)
					{
						var identifier = SyntaxFactory.IdentifierName(InstancePropertyName);

						_instancePropertyAccess = _parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
							(ExpressionSyntax)identifier :
							SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(_parent.Declared.TypeSyntax, identifier));
					}

					return _instancePropertyAccess;
				}
			}

			private StatementSyntax[] _revertableSubscriptions;
			public StatementSyntax[] RevertableSubscriptions
			{
				get
				{
					if (_revertableSubscriptions == null)
					{
						var requiredRevertions = new List<FieldDeclarationSyntax>();
						var expressions = _parent.Declared.SynchronizedFields.Select(f => _factory.GetRevertableSubscription(f, requiredRevertions)).ToArray();

						_revertableSubscriptions = expressions;
					}

					return _revertableSubscriptions;
				}
			}

			private StatementSyntax[] _revertableUnsubscriptions;
			public StatementSyntax[] RevertableUnsubscriptions
			{
				get

				{
					if (_revertableUnsubscriptions == null)
					{
						var requiredRevertions = new List<FieldDeclarationSyntax>();
						var statements = _parent.Declared.SynchronizedFields.Select(f => _factory.GetRevertableUnsubscription(f, requiredRevertions)).ToArray();

						_revertableUnsubscriptions = statements;
					}

					return _revertableUnsubscriptions;
				}
			}

			private StatementSyntax[] _pulls;
			public StatementSyntax[] Pulls => _pulls ?? (_pulls = _parent.Declared.SynchronizedFields.Select(_factory.GetPull).ToArray());

			private StatementSyntax[] _pullAssignments;
			public StatementSyntax[] PullAssignments => _pullAssignments ?? (_pullAssignments = _parent.Declared.SynchronizedFields.Select(_factory.GetPullAssignment).ToArray());
			#endregion
			public ContextInfo(SynchronizedTypeSourceFactory factory, SourceInfo parent)
			{
				_parent = parent ?? throw new ArgumentNullException(nameof(parent));
				_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			}
		}
		internal sealed class MembersInfo
		{
			private readonly SourceInfo _parent;
			private readonly SynchronizedTypeSourceFactory _factory;

			#region Properties
			public String ContextFieldName = "_synchronizationContext";
			public String ContextPropertyName => "SynchronizationContext";

			public String ObservablePropertyPrefix => "Observable";
			public String SynchronizedPropertyPrefix => "Synchronized";

			public String PropertyChangingEventMethodName => "OnPropertyChanging";
			public String PropertyChangedEventMethodName => "OnPropertyChanged";

			public String TypeIdDefaultPropertyName => "TypeId";
			public String TypeIdDefaultPropertySummary =
	@"/// <summary>
/// The Id identifying this instance's type.
/// </summary/>";
			public Boolean TypeIdDefaultPropertyIsStatic => true;
			public Boolean TypeIdDefaultPropertyHasSetter => false;

			public String SourceInstanceIdDefaultPropertyName => "SourceInstanceId";
			public String SourceInstanceIdDefaultPropertySummary =
	@"/// <summary>
/// The Id identifying this instance's property data source.
/// </summary/>";
			public Boolean SourceInstanceIdDefaultPropertyIsStatic => false;
			public Boolean SourceInstanceIdDefaultPropertyHasSetter => true;

			public String InstanceIdDefaultPropertyName => "InstanceId";
			public String InstanceIdDefaultPropertySummary =
	@"/// <summary>
/// The Id identifying this instance.
/// </summary/>";
			public Boolean InstanceIdDefaultPropertyIsStatic => false;
			public Boolean InstanceIdDefaultPropertyHasSetter => false;

			private FieldDeclarationSyntax _contextField;
			public FieldDeclarationSyntax ContextField
			{
				get
				{
					if (_contextField == null)
					{
						_contextField = SyntaxFactory.FieldDeclaration(
							SyntaxFactory.VariableDeclaration(
								SyntaxFactory.ParseTypeName($"{GeneratedSynchronizationClasses.Initializable.Identifier}<{_parent.Context.TypeName}>"))
							.AddVariables(
								SyntaxFactory.VariableDeclarator(ContextFieldName)))
							.AddModifiers(
								SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
								SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
					}

					return _contextField;
				}
			}

			private PropertyDeclarationSyntax _context;
			public PropertyDeclarationSyntax Context
			{
				get
				{
					if (_context == null)
					{
						_context = SyntaxFactory.PropertyDeclaration(_parent.Context.TypeSyntax, ContextPropertyName)
							.AddModifiers(
								SyntaxFactory.Token(
									SyntaxKind.PrivateKeyword))
							.AddAccessorListAccessors(
								SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
								.AddBodyStatements(
									SyntaxFactory.ParseStatement(
$@"if(!this.{ContextField.Declaration.Variables.Single().Identifier}.IsAssigned)
{{
	this.{ContextField.Declaration.Variables.Single().Identifier} = new {_parent.Context.TypeName}(this);
}}
return this.{ContextField.Declaration.Variables.Single().Identifier};")));

					}

					return _context;
				}
			}

			private PropertyDeclarationSyntax _typeId;
			public PropertyDeclarationSyntax TypeId
			{
				get
				{
					if (_typeId == null)
					{
						var typeId = GetIdPropertyDeclaration(_parent.TypeIdAttributeIdentifier,
									   _parent.Members.TypeIdDefaultPropertyName,
									   _parent.Members.TypeIdDefaultPropertySummary,
									   _parent.Members.TypeIdDefaultPropertyIsStatic,
									   _parent.Members.TypeIdDefaultPropertyHasSetter);

						_typeId = typeId;
					}

					return _typeId;
				}
			}

			private ExpressionSyntax _typeIdAccess;
			public ExpressionSyntax TypeIdAccess
			{
				get
				{
					if (_typeIdAccess == null)
					{
						_typeIdAccess = 
					}

					return _typeIdAccess;
				}
			}

			public PropertyDeclarationSyntax GetSourceInstanceIdPropertyDeclaration()
			{
				return GetIdPropertyDeclaration(_info.SourceInstanceIdAttributeIdentifier,
									   _info.Members.SourceInstanceIdDefaultPropertyName,
									   _info.Members.SourceInstanceIdDefaultPropertySummary,
									   _info.Members.SourceInstanceIdDefaultPropertyIsStatic,
									   _info.Members.SourceInstanceIdDefaultPropertyHasSetter);
			}
			public PropertyDeclarationSyntax GetInstanceIdPropertyDeclaration()
			{
				return GetIdPropertyDeclaration(_info.InstanceIdAttributeIdentifier,
									   _info.Members.InstanceIdDefaultPropertyName,
									   _info.Members.InstanceIdDefaultPropertySummary,
									   _info.Members.InstanceIdDefaultPropertyIsStatic,
									   _info.Members.InstanceIdDefaultPropertyHasSetter);
			}



			#endregion

			public MembersInfo(SourceInfo parent)
			{
				_parent = parent ?? throw new ArgumentNullException(nameof(parent));
				_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			}

			private PropertyDeclarationSyntax GetIdPropertyDeclaration(TypeIdentifier idAttributeIdentifier, String fallbackName, String fallbackSummary, Boolean defaultIsStatic, Boolean defaultHasSetter)
			{
				PropertyDeclarationSyntax property;

				if (!TryGetIdProperty(idAttributeIdentifier, out _) && _parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
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
									SyntaxKind.SemicolonToken)));

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
					property = property
						.WithInitializer(
							SyntaxFactory.EqualsValueClause(
								SyntaxFactory.ParseExpression("System.Guid.NewGuid().ToString()")))
						.WithSemicolonToken(
							SyntaxFactory.Token(
								SyntaxKind.SemicolonToken))
						.WithLeadingTrivia(fallbackSummary.Split('\n').Select(SyntaxFactory.Comment));
				}
				else
				{
					property = null;
				};

				return property;
			}

			public Boolean TryGetIdProperty(TypeIdentifier idAttributeIdentifier, out PropertyDeclarationSyntax idProperty)
			{
				var properties = _parent.Declared.Properties.Where(p => p.AttributeLists.HasAttributes(_parent.Declared.SemanticModel, idAttributeIdentifier)).ToArray();
				_factory.ThrowIfMultiple(properties, "properties", idAttributeIdentifier);

				idProperty = properties.SingleOrDefault();

				return idProperty != null;
			}
			public String GetIdPropertyName(TypeIdentifier idAttributeIdentifier, String fallbackName)
			{
				var name = TryGetIdProperty(idAttributeIdentifier, out var property) ?
					property.Identifier.Text :
					fallbackName;

				return name;
			}
			public String GetIdPropertyAccess(TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic)
			{
				var propertyName = GetIdPropertyName(idAttributeIdentifier, fallbackName);

				_ = TryGetIdProperty(idAttributeIdentifier, out var idProperty);
				var isStatic = idProperty?.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)) ?? defaultIsStatic;

				String access = null;

				if (isStatic)
				{
					access = $"{_parent.Declared.TypeIdentifier}.{propertyName}";
				}
				else
				{
					access = propertyName;
				}

				return access;
			}

			public Boolean TryGetTypeIdProperty(out PropertyDeclarationSyntax idProperty)
			{
				return TryGetIdProperty(_info.TypeIdAttributeIdentifier, out idProperty);
			}
			public String GetTypeIdPropertyAccess(Boolean accessingInContext)
			{
				return GetIdPropertyAccess(_info.TypeIdAttributeIdentifier,
								  _info.Members.TypeIdDefaultPropertyName,
								  _info.Members.TypeIdDefaultPropertyIsStatic,
								  accessingInContext);
			}

			public Boolean TryGetSourceInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
			{
				return TryGetIdProperty(_info.SourceInstanceIdAttributeIdentifier, out idProperty);
			}
			public String GetSourceInstanceIdPropertyAccess(Boolean accessingInContext)
			{
				return GetIdPropertyAccess(_info.SourceInstanceIdAttributeIdentifier,
								  _info.Members.SourceInstanceIdDefaultPropertyName,
								  _info.Members.SourceInstanceIdDefaultPropertyIsStatic,
								  accessingInContext);
			}

			public Boolean TryGetInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
			{
				return TryGetIdProperty(_info.InstanceIdAttributeIdentifier, out idProperty);
			}
			public String GetInstanceIdPropertyAccess(Boolean accessingInContext)
			{
				return GetIdPropertyAccess(_info.InstanceIdAttributeIdentifier,
								  _info.Members.InstanceIdDefaultPropertyName,
								  _info.Members.InstanceIdDefaultPropertyIsStatic,
								  accessingInContext);
			}
		}

		#region Aliae
		public TypeIdentifier TypeIdAttributeIdentifier => GeneratedAttributes.TypeId.GeneratedType.Identifier;
		public TypeIdentifier InstanceIdAttributeIdentifier => GeneratedAttributes.InstanceId.GeneratedType.Identifier;
		public TypeIdentifier SourceInstanceIdAttributeIdentifier => GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier;
		public TypeIdentifier SynchronizationAuthorityAttributeIdentifier => GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier;
		public TypeIdentifier SynchronizationTargetAttributeIdentifier => GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier;
		public TypeIdentifier SynchronizedAttributeIdentifier => GeneratedAttributes.Synchronized.GeneratedType.Identifier;

		public IAttributeFactory<TypeIdAttribute> TypeIdAttributeFactory => GeneratedAttributes.TypeId.Factory;
		public IAttributeFactory<InstanceIdAttribute> InstanceIdAttributeFactory => GeneratedAttributes.InstanceId.Factory;
		public IAttributeFactory<SourceInstanceIdAttribute> SourceInstanceIdAttributeFactory => GeneratedAttributes.SourceInstanceId.Factory;
		public IAttributeFactory<SynchronizationAuthorityAttribute> SynchronizationAuthorityAttributeFactory => GeneratedAttributes.SynchronizationAuthority.Factory;
		public IAttributeFactory<SynchronizationTargetAttribute> SynchronizationTargetAttributeFactory => GeneratedAttributes.SynchronizationTarget.Factory;
		public IAttributeFactory<SynchronizedAttribute> SynchronizedAttributeFactory => GeneratedAttributes.Synchronized.Factory;

		public TypeIdentifier ISynchronizationAuthorityIdentifier => GeneratedSynchronizationClasses.ISynchronizationAuthority.Identifier;
		#endregion

		public DeclaredInfo Declared { get; }
		public ContextInfo Context { get; }
		public MembersInfo Members { get; }

		public SourceInfo(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel, SynchronizedTypeSourceFactory factory)
		{
			Members = new MembersInfo(this);
			Declared = new DeclaredInfo(synchronizedType, semanticModel, this);
			Context = new ContextInfo(factory, this);
		}

	}
}
