using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObjectSync.Generator
{
	internal sealed partial class SourceFactory
	{
		private sealed class ContextFactory
		{
			private readonly SourceFactory _parent;

			#region Constants
			public String ConstructorParameterName => "instance";
			public String TypeSuffix => "SynchronizationContext";
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

			public String SourceInstanceIdPropertyName => "SourceInstanceId";
			public String SourceInstanceIdPropertySummary =>
	@"/// <summary>
/// Provides the source instance id for the instance.
/// </summary>";

			public String InstanceIdPropertyName => "InstanceId";
			public String InstanceIdPropertySummary =>
	@"/// <summary>
/// Provides the instance id for the instance.
/// </summary>";

			public String SyncRootPropertyName => "SyncRoot";
			public String SyncRootSummary => @"/// <summary>
/// Sync object for synchronizing access to synchronization logic.
/// </summary>";

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

			public String DesynchronizeInvokeSynchronizeMethodName => "DesynchronizeInvokeSynchronize";
			public String DesynchronizeInvokeSynchronizeMethodSummary =>
@"/// <summary>
/// If <paramref name=""method""/> is not null, desynchronizes the instance, invokes <paramref name=""method""/>, and synchronizes the instance again, all in a threadsafe manner.
/// This means that the synchronization state of the instance is guaranteed not to change during the invocation.
/// <para>
/// Invoking any method of this instance in <paramref name=""method""/> will likely cause a deadlock to occur.
/// </para>
/// </summary>
/// <param name=""method"">The method to invoke after desynchronizing.</param>";

			public String LocalTypeIdName => "typeId";
			public String LocalSourceInstanceIdName => "sourceInstanceId";
			public String LocalInstanceIdName => "instanceId";
			public String LocalAuthorityName => "authority";
			public String LocalOnRevertName => "onRevert";
			public String LocalFieldName => "fieldName";
			public String LocalFieldTypeName => "TField";
			public String LocalValueName => "value";

			public String ResynchronizeMethodName => "Resynchronize";
			public String ResynchronizeMethodSummary =>
	@"/// <summary>
/// Synchronizes the instance.
/// If it is synchronized already, it is first desynchronized.
/// </summary>";

			public String PullValuePrefix => "valueOf";

			public String PushMethodName => "Push";
			public String PushMethodSummary =>
@"/// <summary>
/// Pushes the value provided to the wrapped synchronization authority.
/// </summary>";
			#endregion

			#region Properties
			private String _typeName;
			public String TypeName
			{
				get
				{
					if (_typeName == null)
					{
						var parts = _parent.Declared.TypeIdentifier.Name.Parts.ToArray();
						int i = -1;
						for(;++i < parts.Length-1 && !(i < parts.Length - 2 && parts[i + 1].Kind == IdentifierPart.PartKind.GenericOpen);) { }
						
						_typeName = $"{parts[i]}{TypeSuffix}";
					}

					return _typeName;
				}
			}

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
						var expressions = _parent.Declared.SynchronizedFields.Select(f => GetRevertableSubscription(f, requiredRevertions, true)).ToArray();

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
						var statements = _parent.Declared.SynchronizedFields.Select(f => GetRevertableUnsubscription(f, requiredRevertions, true)).ToArray();

						_revertableUnsubscriptions = statements;
					}

					return _revertableUnsubscriptions;
				}
			}

			private StatementSyntax[] _pulls;
			public StatementSyntax[] Pulls => _pulls ?? (_pulls = _parent.Declared.SynchronizedFields.Select(GetPull).ToArray());

			private StatementSyntax[] _pullAssignments;
			public StatementSyntax[] PullAssignments => _pullAssignments ?? (_pullAssignments = _parent.Declared.SynchronizedFields.Select(GetPullAssignment).ToArray());

			private ExpressionSyntax _typeIdAccess;
			public ExpressionSyntax TypeIdAccess
			{
				get
				{
					return _typeIdAccess ?? (_typeIdAccess = SyntaxFactory.ParseExpression($"this.{_parent.Context.TypeIdPropertyName}"));
				}
			}

			private ExpressionSyntax _sourceInstanceIdAccess;
			public ExpressionSyntax SourceInstanceIdAccess
			{
				get
				{
					return _sourceInstanceIdAccess ?? (_sourceInstanceIdAccess = SyntaxFactory.ParseExpression($"this.{_parent.Context.SourceInstanceIdPropertyName}"));
				}
			}

			private ExpressionSyntax _instanceIdAccess;
			public ExpressionSyntax InstanceIdAccess
			{
				get
				{
					return _instanceIdAccess ?? (_instanceIdAccess = SyntaxFactory.ParseExpression($"this.{InstanceIdPropertyName}"));
				}
			}

			private ExpressionSyntax _authorityAccess;
			public ExpressionSyntax AuthorityAccess
			{
				get
				{
					return _authorityAccess ?? (_authorityAccess = SyntaxFactory.ParseExpression($"this.{_parent.Context.AuthorityPropertyName}"));
				}
			}
			#endregion
			public ContextFactory(SourceFactory parent)
			{
				_parent = parent ?? throw new ArgumentNullException(nameof(parent));
			}

			private SyntaxToken[] GetTypeModifiers()
			{
				IEnumerable<SyntaxKind> kinds = _parent.Declared.SynchronizationTargetAttribute.ContextTypeAccessibility.AsSyntax();

				if (_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed)
				{
					kinds = kinds.Append(SyntaxKind.SealedKeyword);
				}

				var tokens = kinds.Select(SyntaxFactory.Token).ToArray();

				var contextTypeModifiers = new Optional<SyntaxToken[]>(tokens);

				return contextTypeModifiers.Value;
			}
			public MemberDeclarationSyntax GetDeclaration()
			{
				var kind = _parent.Declared.Type.Kind();
				var name = TypeName;
				var modifiers = GetTypeModifiers();
				var members = GetMembers();

				var contextTypeDeclaration = SyntaxFactory.TypeDeclaration(kind, name)
					.AddModifiers(modifiers)
					.AddMembers(members);

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					contextTypeDeclaration = contextTypeDeclaration.WithBaseList(
						SyntaxFactory.BaseList()
						.AddTypes(
							SyntaxFactory.SimpleBaseType(
								SyntaxFactory.ParseTypeName(_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName))));
				}

				contextTypeDeclaration = contextTypeDeclaration
						.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia("#nullable disable\r\n"))
						.WithTrailingTrivia(SyntaxFactory.ParseLeadingTrivia("\r\n#nullable restore"));

				var t = contextTypeDeclaration.NormalizeWhitespace().ToFullString();

				return contextTypeDeclaration;
			}
			public MemberDeclarationSyntax[] GetMembers()
			{
				var members = new MemberDeclarationSyntax[]
				{
					GetEvent(),
					GetIsSynchronizedField(),
					GetIsSynchronizedProperty(),
					GetInstanceProperty(),
					GetSyncRootProperty(),
					GetConstructor(),
					GetInvokeMethod(),
					GetDesynchronizeMethod(),
					GetDesynchronizeUnlockedMethod(),
					GetSynchronizeMethod(),
					GetSynchronizeUnlockedMethod(),
					GetResynchronizeMethod(),
					GetDesynchronizeInvokeSynchronizeMethod(),
					GetAuthorityProperty(),
					GetTypeIdProperty(),
					GetSourceInstanceIdProperty(),
					GetInstanceIdProperty(),
					GetPushMethod()
				}
				.Where(m => m != null)
				.OrderBy(m => m.GetType().Name)
				.ToArray();

				return members;
			}

			private ConstructorDeclarationSyntax GetConstructor()
			{
				var synchronizedTypeName = _parent.Declared.TypeIdentifier;
				var contextTypeName = TypeName;

				var parameterName = ConstructorParameterName;

				var constructor = SyntaxFactory.ConstructorDeclaration(contextTypeName)
					.AddModifiers(_parent.Declared.SynchronizationTargetAttribute.ContextTypeConstructorAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
					.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)).WithType(_parent.Declared.TypeSyntax));

				constructor = _parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null
					? constructor.AddBodyStatements(
						SyntaxFactory.ParseStatement(text: $"this.{InstancePropertyName} = {parameterName};"))
					: constructor.WithInitializer(
						SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
							.AddArgumentListArguments(
									SyntaxFactory.Argument(
										SyntaxFactory.ParseExpression(parameterName))))
					.AddBodyStatements();

				return constructor;
			}
			private PropertyDeclarationSyntax GetInstanceProperty()
			{
				PropertyDeclarationSyntax property = null;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
						.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

					if (_parent.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.Private &&
						_parent.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.NotApplicable)
					{
						setter = setter.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
					}

					property = SyntaxFactory.PropertyDeclaration(_parent.Declared.TypeSyntax, InstancePropertyName)
						.AddModifiers(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword))
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
									.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
							setter)
						.WithLeadingTrivia(InstancePropertySummary.AsLeadingTrivia());
				}

				return property;
			}
			private FieldDeclarationSyntax GetIsSynchronizedField()
			{
				FieldDeclarationSyntax field;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					field = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Int32>())))
						.AddDeclarationVariables(SyntaxFactory.VariableDeclarator(IsSynchronizedFieldName))
						.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
						.WithLeadingTrivia(IsSynchronizedFieldSummary.AsLeadingTrivia());
				}
				else
				{
					field = null;
				}

				return field;
			}
			private PropertyDeclarationSyntax GetIsSynchronizedProperty()
			{
				PropertyDeclarationSyntax property;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					property = SyntaxFactory.PropertyDeclaration(
							SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Boolean>()),
							IsSynchronizedPropertyName)
						.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
							.AddBodyStatements(
								SyntaxFactory.ParseStatement("var valueInt = value?1:0;"),
								SyntaxFactory.ParseStatement($"var requiredValueInt = this.{IsSynchronizedFieldName}/* == 1?0:1*/;"),
								SyntaxFactory.ParseStatement(
	$@"if(System.Threading.Interlocked.CompareExchange(ref {IsSynchronizedFieldName}, valueInt, requiredValueInt) == requiredValueInt)
{{
	this.{IsSynchronizedFieldName} = valueInt;
	this.{EventName}?.Invoke(this, value);
}}")),
							SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
							.AddBodyStatements(
								SyntaxFactory.ParseStatement(
	$@"return this.{IsSynchronizedFieldName} == 1;")))
						.WithLeadingTrivia(IsSynchronizedPropertySummary.AsLeadingTrivia());
				}
				else
				{
					property = null;
				}

				return property;
			}
			private EventFieldDeclarationSyntax GetEvent()
			{
				EventFieldDeclarationSyntax @event;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					@event = SyntaxFactory.EventFieldDeclaration(
						SyntaxFactory.VariableDeclaration(
							SyntaxFactory.ParseTypeName(TypeIdentifier.Create<EventHandler<Boolean>>())))
						.AddDeclarationVariables(
							SyntaxFactory.VariableDeclarator(EventName))
						.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
						.WithLeadingTrivia(EventSummary.AsLeadingTrivia());
				}
				else
				{
					@event = null;
				}

				return @event;
			}
			private PropertyDeclarationSyntax GetSyncRootProperty()
			{
				PropertyDeclarationSyntax property;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					property = SyntaxFactory.PropertyDeclaration(
							SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Object>()),
							SyncRootPropertyName)
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
							.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
						.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
						.WithLeadingTrivia(SyncRootSummary.AsLeadingTrivia())
						.WithInitializer(
							SyntaxFactory.EqualsValueClause(
								SyntaxFactory.ParseExpression($"new {TypeIdentifier.Create<Object>()}()")))
						.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
				}
				else
				{
					property = null;
				}

				return property;
			}
			private MethodDeclarationSyntax GetInvokeMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						InvokeMethodName)
					.AddModifiers(
						SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddParameterListParameters(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(InvokeMethodMethodParameterName))
						.WithType(
							SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action<Boolean>>())))
					.AddBodyStatements(
						SyntaxFactory.ParseStatement(
	$@"if({InvokeMethodMethodParameterName} != null)
{{
	lock(this.{SyncRootPropertyName})
	{{
		{InvokeMethodMethodParameterName}.Invoke({IsSynchronizedPropertyName});
	}}
}}"))
					.WithLeadingTrivia(InvokeMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}
			private MethodDeclarationSyntax GetDesynchronizeMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						DesynchronizeMethodName)
					.AddModifiers(
						SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddBodyStatements(
						SyntaxFactory.ParseStatement(
	$@"if (this.{IsSynchronizedPropertyName})
{{
	lock ({SyncRootPropertyName})
	{{
		if (this.{IsSynchronizedPropertyName})
		{{
			var {LocalAuthorityName} = {AuthorityPropertyName};
			if (authority != null)
			{{
				var {LocalTypeIdName} = {TypeIdAccess};
				var {LocalSourceInstanceIdName} = {SourceInstanceIdAccess};
				var {LocalInstanceIdName} = {InstanceIdAccess};

				{GetRevertableSyncUnlockedMethodCall(DesynchronizeUnlockedMethodName)}
			}}

			{IsSynchronizedPropertyName} = false;
		}}
	}}
}}"))
					.WithLeadingTrivia(DesynchronizeMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}
			private MethodDeclarationSyntax GetDesynchronizeUnlockedMethod()
			{
				var method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						DesynchronizeUnlockedMethodName)
					.AddParameterListParameters(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalTypeIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalSourceInstanceIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalInstanceIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalAuthorityName))
						.WithType(
							_parent.ISynchronizationAuthorityIdentifier.AsSyntax()),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalOnRevertName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())));

				method = method
					.AddBodyStatements(RevertableUnsubscriptions)
					.WithLeadingTrivia(DesynchronizeUnlockedMethodSummary.AsLeadingTrivia());

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					method = method
						.AddBodyStatements(
							SyntaxFactory.ParseStatement($"{GetRevertableSyncUnlockedMethodCall(DesynchronizeUnlockedMethodName, "base", $"() => {GetRevertableSyncUnlockedMethodCall(SynchronizeUnlockedMethodName, setSemicolon: false)}")}"));
				}

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null &&
					_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed)
				{
					method = method.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.PrivateKeyword));
				}
				else
				{
					method = method.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.ProtectedKeyword),
						SyntaxFactory.Token(
							_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.VirtualKeyword :
							SyntaxKind.OverrideKeyword));
				}

				return method;
			}
			private MethodDeclarationSyntax GetSynchronizeMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						SynchronizeMethodName)
					.AddModifiers(
						SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddBodyStatements(
						SyntaxFactory.ParseStatement(
	$@"if (!this.{IsSynchronizedPropertyName})
{{
	lock ({SyncRootPropertyName})
	{{
		if (!this.{IsSynchronizedPropertyName})
		{{
			var {LocalAuthorityName} = {AuthorityAccess};
			if ({LocalAuthorityName} != null)
			{{
				var {LocalTypeIdName} = {TypeIdAccess};
				var {LocalSourceInstanceIdName} = {SourceInstanceIdAccess};
				var {LocalInstanceIdName} = {InstanceIdAccess};

				{GetRevertableSyncUnlockedMethodCall(SynchronizeUnlockedMethodName)}
			}}

			{IsSynchronizedPropertyName} = true;
		}}
	}}
}}"))
					.WithLeadingTrivia(SynchronizeMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}
			private MethodDeclarationSyntax GetSynchronizeUnlockedMethod()
			{
				var method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						SynchronizeUnlockedMethodName)
					.AddParameterListParameters(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalTypeIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalSourceInstanceIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalInstanceIdName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalAuthorityName))
						.WithType(
							_parent.ISynchronizationAuthorityIdentifier.AsSyntax()),
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(LocalOnRevertName))
						.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())));

				method = method
					.AddBodyStatements(RevertableSubscriptions)
					.AddBodyStatements(
						SyntaxFactory.ParseStatement($@"{String.Join("\r\n", Pulls.Select(s => s.ToFullString()))}"),
						SyntaxFactory.ParseStatement($"{String.Join("\r\n", PullAssignments.Select(s => s.ToFullString()))}"))
					.WithLeadingTrivia(SynchronizeUnlockedMethodSummary.AsLeadingTrivia());

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					method = method
						.AddBodyStatements(
							SyntaxFactory.ParseStatement($"{GetRevertableSyncUnlockedMethodCall(SynchronizeUnlockedMethodName, "base", $"() => {GetRevertableSyncUnlockedMethodCall(DesynchronizeUnlockedMethodName, setSemicolon: false)}")}"));
				}

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null &&
					_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed)
				{
					method = method.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.PrivateKeyword));
				}
				else
				{
					method = method.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.ProtectedKeyword),
						SyntaxFactory.Token(
							_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.VirtualKeyword :
							SyntaxKind.OverrideKeyword));
				}

				return method;
			}
			private MethodDeclarationSyntax GetResynchronizeMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						ResynchronizeMethodName)
					.AddBodyStatements(
							SyntaxFactory.ParseStatement(
$@"lock(this.{SyncRootPropertyName})
{{	
	var {LocalAuthorityName} = {AuthorityAccess};

	if ({LocalAuthorityName} != null)
	{{
		var {LocalTypeIdName} = {TypeIdAccess};
		var {LocalSourceInstanceIdName} = {SourceInstanceIdAccess};
		var {LocalInstanceIdName} = {InstanceIdAccess};

		if (this.{IsSynchronizedPropertyName})
		{{
			{GetRevertableSyncUnlockedMethodCall(DesynchronizeUnlockedMethodName)}

			this.{IsSynchronizedPropertyName} = false;
		}}

		{GetRevertableSyncUnlockedMethodCall(SynchronizeUnlockedMethodName)}

		{String.Join("\r\n", Pulls.Select(s => s.ToFullString()))}

		{String.Join("\r\n", PullAssignments.Select(s => s.ToFullString()))}
	}}
	this.{IsSynchronizedPropertyName} = true;
}}"))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.WithLeadingTrivia(ResynchronizeMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}
			private MethodDeclarationSyntax GetDesynchronizeInvokeSynchronizeMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
						SyntaxFactory.ParseTypeName("void"),
						DesynchronizeInvokeSynchronizeMethodName)
					.AddParameterListParameters(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier(InvokeMethodMethodParameterName))
						.WithType(
							SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())))
					.AddBodyStatements(
							SyntaxFactory.ParseStatement(
$@"if({InvokeMethodMethodParameterName} != null)
{{
	lock(this.{SyncRootPropertyName})
	{{
		var {LocalAuthorityName} = {AuthorityAccess};
		if (authority != null)
		{{
			var {LocalTypeIdName} = {TypeIdAccess};
			var {LocalSourceInstanceIdName} = {SourceInstanceIdAccess};
			var {LocalInstanceIdName} = {InstanceIdAccess};

			if (this.{IsSynchronizedPropertyName})
			{{
				{GetRevertableSyncUnlockedMethodCall(DesynchronizeUnlockedMethodName)}

				this.{IsSynchronizedPropertyName} = false;
			}}

			{InvokeMethodMethodParameterName}.Invoke();

			{GetRevertableSyncUnlockedMethodCall(SynchronizeUnlockedMethodName)}

			{String.Join("\r\n", Pulls.Select(s => s.ToFullString()))}

			{String.Join("\r\n", PullAssignments.Select(s => s.ToFullString()))}

			this.{IsSynchronizedPropertyName} = true;
		}}
		else
		{{
			{InvokeMethodMethodParameterName}.Invoke();

			this.{IsSynchronizedPropertyName} = true;
		}}
	}}
}}"))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.WithLeadingTrivia(DesynchronizeInvokeSynchronizeMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}
			private MethodDeclarationSyntax GetPushMethod()
			{
				MethodDeclarationSyntax method;

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					method = SyntaxFactory.MethodDeclaration(
							SyntaxFactory.ParseTypeName("void"),
							PushMethodName)
						.AddModifiers(
							SyntaxFactory.Token(SyntaxKind.PublicKeyword))
						.AddTypeParameterListParameters(
							SyntaxFactory.TypeParameter(
								SyntaxFactory.Identifier(LocalFieldTypeName)))
						.AddParameterListParameters(
							SyntaxFactory.Parameter(
								SyntaxFactory.Identifier(LocalFieldName))
							.WithType(
								TypeIdentifier.Create<String>().AsSyntax()))
						.AddParameterListParameters(
							SyntaxFactory.Parameter(
								SyntaxFactory.Identifier(LocalValueName))
							.WithType(
								SyntaxFactory.ParseTypeName(LocalFieldTypeName)))
						.AddBodyStatements(
							SyntaxFactory.ParseStatement($"{AuthorityAccess}" +
														 $"?.Push<{LocalFieldTypeName}" +
														 $">({TypeIdAccess}" +
														 $", {LocalFieldName}" +
														 $", {SourceInstanceIdAccess}" +
														 $", {InstanceIdAccess}" +
														 $", {LocalValueName});"))
						.WithLeadingTrivia(PushMethodSummary.AsLeadingTrivia());
				}
				else
				{
					method = null;
				}

				return method;
			}

			private PropertyDeclarationSyntax GetAuthorityProperty()
			{
				PropertyDeclarationSyntax property;

				if (_parent.Declared.Authority != null)
				{
					property = SyntaxFactory.PropertyDeclaration(
							_parent.ISynchronizationAuthorityIdentifier.AsSyntax(),
							AuthorityPropertyName)
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(
								SyntaxKind.GetAccessorDeclaration)
							.AddBodyStatements(
								SyntaxFactory.ParseStatement(
									$"return {InstancePropertyAccess}.{_parent.Declared.Authority.Identifier};")))
						.AddModifiers(SyntaxFactory.Token(
								_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed &&
								_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
								SyntaxKind.PrivateKeyword :
								SyntaxKind.ProtectedKeyword));

					if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
					{
						property = property
							.AddModifiers(
								SyntaxFactory.Token(
									SyntaxKind.OverrideKeyword));
					}

					property = property.WithLeadingTrivia(AuthorityPropertySummary.AsLeadingTrivia());
				}
				else if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					throw new Exception($"Either {_parent.Declared.TypeIdentifier} or one of its synchronized base classes must provide a property annotated with {_parent.SynchronizationAuthorityAttributeIdentifier}.");
				}
				else
				{
					property = null;
				}

				return property;
			}
			private PropertyDeclarationSyntax GetTypeIdProperty()
			{
				return GetIdProperty(_parent.Declared.TypeId, _parent.Members.TypeId, TypeIdPropertyName, TypeIdPropertySummary);
			}
			private PropertyDeclarationSyntax GetSourceInstanceIdProperty()
			{
				return GetIdProperty(_parent.Declared.SourceInstanceId, _parent.Members.SourceInstanceId, SourceInstanceIdPropertyName, SourceInstanceIdPropertySummary);
			}
			private PropertyDeclarationSyntax GetInstanceIdProperty()
			{
				return GetIdProperty(_parent.Declared.InstanceId, _parent.Members.InstanceId, InstanceIdPropertyName, InstanceIdPropertySummary);
			}

			private StatementSyntax GetUnsubscription(FieldDeclarationSyntax field)
			{
				var fieldName = _parent.GetFieldName(field);

				var statement = SyntaxFactory.ParseStatement($"{_parent.Context.LocalAuthorityName}.Unsubscribe(" +
															 $"{_parent.Context.LocalTypeIdName}, \"" +
															 $"{fieldName}\", " +
															 $"{_parent.Context.LocalSourceInstanceIdName}, " +
															 $"{_parent.Context.LocalInstanceIdName});");

				return statement;
			}
			private StatementSyntax GetRevertableUnsubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions, Boolean withOnRevert)
			{
				requiredRevertions.Add(field);

				var revertions = requiredRevertions.Select(f => GetSubscription(f));

				if (withOnRevert)
				{
					revertions = revertions.Append(SyntaxFactory.ParseStatement($"{_parent.Context.LocalOnRevertName}?.Invoke();"));
				}

				var statement = SyntaxFactory.TryStatement()
					.AddBlockStatements(
						GetUnsubscription(field))
					.AddCatches(
						SyntaxFactory.CatchClause()
							.AddBlockStatements(
								revertions
								.Append(SyntaxFactory.ThrowStatement())
								.ToArray()));

				return statement;
			}

			private StatementSyntax GetRevertableSubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions, Boolean withOnRevert)
			{
				requiredRevertions.Add(field);

				var revertions = requiredRevertions.Select(f => GetUnsubscription(f));

				if (withOnRevert)
				{
					revertions = revertions.Append(SyntaxFactory.ParseStatement($"{_parent.Context.LocalOnRevertName}?.Invoke();"));
				}

				var statement = SyntaxFactory.TryStatement()
					.AddBlockStatements(
						GetSubscription(field))
					.AddCatches(
						SyntaxFactory.CatchClause()
							.AddBlockStatements(
								revertions
								.Append(SyntaxFactory.ThrowStatement())
								.ToArray()));

				return statement;
			}
			private StatementSyntax GetSubscription(FieldDeclarationSyntax field)
			{
				var fieldType = _parent.GetFieldType(field);

				var fieldName = _parent.GetFieldName(field);
				var setStatement = _parent.GetSetStatement(field, fromWithinContext: true);

				var statement = SyntaxFactory.ParseStatement($"{_parent.Context.LocalAuthorityName}.Subscribe<" +
															 $"{fieldType}>(" +
															 $"{_parent.Context.LocalTypeIdName}, \"" +
															 $"{fieldName}\", " +
															 $"{_parent.Context.LocalSourceInstanceIdName}, " +
															 $"{_parent.Context.LocalInstanceIdName}, (value) => {{" +
															 $"{setStatement}}});");

				return statement;
			}

			private StatementSyntax GetPull(FieldDeclarationSyntax field)
			{
				var fieldType = _parent.GetFieldType(field);
				var fieldName = _parent.GetFieldName(field);
				var propertyName = _parent.GetGeneratedPropertyName(field);

				var statement = SyntaxFactory.ParseStatement($"var {_parent.Context.PullValuePrefix}{propertyName} = " +
															 $"{_parent.Context.LocalAuthorityName}.Pull<" +
															 $"{fieldType}>(" +
															 $"{_parent.Context.LocalTypeIdName}, \"" +
															 $"{fieldName}\", " +
															 $"{_parent.Context.LocalSourceInstanceIdName}, " +
															 $"{_parent.Context.LocalInstanceIdName});");

				return statement;
			}
			private StatementSyntax GetPullAssignment(FieldDeclarationSyntax field)
			{
				var propertyName = _parent.GetGeneratedPropertyName(field);
				var fieldName = _parent.GetFieldName(field);

				var statement = SyntaxFactory.ParseStatement($"{_parent.Context.InstancePropertyAccess}." +
															 $"{fieldName} = " +
															 $"{_parent.Context.PullValuePrefix}{propertyName};");

				return statement;
			}

			private String GetRevertableSyncUnlockedMethodCall(String methodName, String instance = "this", String onRevert = "null", Boolean setSemicolon = true)
			{
				var statement = $"{instance}.{methodName}({LocalTypeIdName}:{LocalTypeIdName}, {LocalSourceInstanceIdName}:{LocalSourceInstanceIdName}, {LocalInstanceIdName}:{LocalInstanceIdName}, {LocalAuthorityName}:{LocalAuthorityName}, {LocalOnRevertName}:{onRevert}){(setSemicolon ? ";" : String.Empty)}";

				return statement;
			}

			private PropertyDeclarationSyntax GetIdProperty(PropertyDeclarationSyntax declared, PropertyDeclarationSyntax member, String name, String summary)
			{
				PropertyDeclarationSyntax property;

				Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> getProperty = (id) =>
				{
					var access = id.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ?
						$"{_parent.Declared.TypeIdentifier}.{id.Identifier}" :
						$"{InstancePropertyAccess}.{id.Identifier}";

					return SyntaxFactory.PropertyDeclaration(
								TypeIdentifier.Create<String>().AsSyntax(),
								name)
							.AddAccessorListAccessors(
								SyntaxFactory.AccessorDeclaration(
									SyntaxKind.GetAccessorDeclaration)
								.AddBodyStatements(
									SyntaxFactory.ParseStatement(
										$"return {access};")))
							.AddModifiers(
								SyntaxFactory.Token(
									_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed &&
									_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
									SyntaxKind.PrivateKeyword :
									SyntaxKind.ProtectedKeyword))
							.WithLeadingTrivia(summary.AsLeadingTrivia());
				};

				if (_parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
				{
					property = getProperty(declared ?? member);

					if (!_parent.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed)
					{
						property = property
							.AddModifiers(
								SyntaxFactory.Token(
									SyntaxKind.VirtualKeyword));
					}
				}
				else
				{
					if (declared != null)
					{
						property = getProperty(declared)
							.AddModifiers(
								SyntaxFactory.Token(
									SyntaxKind.OverrideKeyword));
					}
					else
					{
						property = null;
					}
				}

				return property;
			}
		}

	}
}
