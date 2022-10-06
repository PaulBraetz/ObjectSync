using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
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
		private Optional<GeneratedSource> _generatedSource;
		private readonly SourceInfo _info;

		public SynchronizedTypeSourceFactory(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel)
		{
			_info = new SourceInfo(synchronizedType, semanticModel, this);
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
				var name = _info.Declared.TypeIdentifier;

				try
				{
					var source = GetNamespaceDeclaration();

					_generatedSource = new GeneratedSource(source, name);
				}
				catch (Exception ex)
				{
					var source =
$@"/*
An error occured while generating this source file for {_info.Declared.TypeIdentifier}:
{ex}
*/";
					_generatedSource = new GeneratedSource(source, name);
				}
			}

			return _generatedSource.Value;
		}

		#region Type
		public NamespaceDeclarationSyntax GetNamespaceDeclaration()
		{
			var namespaceName = TryGetNamespace(_info.Declared.Type, out var declaredNamespace) ?
				declaredNamespace.Name :
				throw new Exception($"{_info.Declared.TypeIdentifier} was not declared in a namespace.");

			var generatedTypeDeclaration = GetGeneratedTypeDeclaration();

			var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(namespaceName)
					.AddMembers(generatedTypeDeclaration);

			return namespaceDeclaration;
		}
		public MemberDeclarationSyntax[] GetGeneratedTypeMembers()
		{
			var members = new MemberDeclarationSyntax[]
				{
					GetContextDeclaration()
				}.Concat(GetIdDeclarations())
				.Where(m => m != null)
				.ToArray();

			return members;
		}
		public BaseTypeDeclarationSyntax GetGeneratedTypeDeclaration()
		{
			var synchronizedTypeDeclarationName = _info.Declared.TypeIdentifier;
			var generatedTypeMembers = GetGeneratedTypeMembers();

			var generatedTypeDeclaration = SyntaxFactory.TypeDeclaration(SyntaxKind.ClassDeclaration, synchronizedTypeDeclarationName)
				.WithModifiers(_info.Declared.Type.Modifiers)
				.WithMembers(new SyntaxList<MemberDeclarationSyntax>(generatedTypeMembers));

			return generatedTypeDeclaration;
		}
		#endregion

		#region Context
		public SyntaxToken[] GetContextTypeModifiers()
		{
			IEnumerable<SyntaxKind> kinds = _info.Declared.SynchronizationTargetAttribute.ContextTypeAccessibility.AsSyntax();

			if (_info.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed)
			{
				kinds = kinds.Append(SyntaxKind.SealedKeyword);
			}

			var tokens = kinds.Select(SyntaxFactory.Token).ToArray();

			var contextTypeModifiers = new Optional<SyntaxToken[]>(tokens);

			return contextTypeModifiers.Value;
		}
		public MemberDeclarationSyntax GetContextDeclaration()
		{
			var kind = _info.Declared.Type.Kind();
			var name = _info.Context.TypeName;
			var modifiers = GetContextTypeModifiers();
			var members = GetContextMembers();

			var contextTypeDeclaration = SyntaxFactory.TypeDeclaration(kind, name)
				.AddModifiers(modifiers)
				.AddMembers(members);

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
			{
				contextTypeDeclaration = contextTypeDeclaration.WithBaseList(
					SyntaxFactory.BaseList()
					.AddTypes(
						SyntaxFactory.SimpleBaseType(
							SyntaxFactory.ParseTypeName(_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName))));
			}

			return contextTypeDeclaration;
		}
		public MemberDeclarationSyntax[] GetContextMembers()
		{
			var members = new MemberDeclarationSyntax[]
			{
				GetContextEvent(),
				GetContextIsSynchronizedField(),
				GetContextIsSynchronizedProperty(),
				GetContextInstanceProperty(),
				GetContextSyncRootProperty(),
				GetContextConstructor(),
				GetContextInvokeMethod(),
				GetContextDesynchronizeMethod(),
				GetContextDesynchronizeUnlockedMethod(),
				GetContextSynchronizeMethod(),
				GetContextSynchronizeUnlockedMethod(),
				GetContextResynchronizeMethod(),
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

		public ConstructorDeclarationSyntax GetContextConstructor()
		{
			var synchronizedTypeName = _info.Declared.TypeIdentifier;
			var contextTypeName = _info.Context.TypeName;

			var parameterName = _info.Context.ConstructorParameterName;

			var constructor = SyntaxFactory.ConstructorDeclaration(contextTypeName)
				.AddModifiers(_info.Declared.SynchronizationTargetAttribute.ContextTypeConstructorAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
				.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)).WithType(_info.Declared.TypeSyntax));

			constructor = _info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null
				? constructor.AddBodyStatements(
					SyntaxFactory.ParseStatement(text: $"this.{_info.Context.InstancePropertyName} = {parameterName};"))
				: constructor.WithInitializer(
					SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
						.AddArgumentListArguments(
								SyntaxFactory.Argument(
									SyntaxFactory.ParseExpression(parameterName))))
				.AddBodyStatements();

			return constructor;
		}
		public PropertyDeclarationSyntax GetContextInstanceProperty()
		{
			PropertyDeclarationSyntax property = null;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
					.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

				if (_info.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.Private &&
					_info.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility != Attributes.Attributes.Accessibility.NotApplicable)
				{
					setter = setter.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
				}

				property = SyntaxFactory.PropertyDeclaration(_info.Declared.TypeSyntax, _info.Context.InstancePropertyName)
					.WithAccessorList(
						SyntaxFactory.AccessorList()
						.AddAccessors(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
							.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
						.AddAccessors(setter))
					.AddModifiers(_info.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
					.WithLeadingTrivia(_info.Context.InstancePropertySummary.Split('\n').Select(SyntaxFactory.Comment));
			}

			return property;
		}
		public FieldDeclarationSyntax GetContextIsSynchronizedField()
		{
			FieldDeclarationSyntax field;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				field = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Int32>())))
					.AddDeclarationVariables(SyntaxFactory.VariableDeclarator(_info.Context.IsSynchronizedFieldName))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
					.WithLeadingTrivia(_info.Context.IsSynchronizedFieldSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				field = null;
			}

			return field;
		}
		public PropertyDeclarationSyntax GetContextIsSynchronizedProperty()
		{
			PropertyDeclarationSyntax property;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Boolean>()),
						_info.Context.IsSynchronizedPropertyName)
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement("var valueInt = value?1:0;"),
							SyntaxFactory.ParseStatement($"var requiredValueInt = this.{_info.Context.IsSynchronizedFieldName} == 1?0:1;"),
							SyntaxFactory.ParseStatement(
$@"if(System.Threading.Interlocked.CompareExchange(ref {_info.Context.IsSynchronizedFieldName}, valueInt, requiredValueInt) == requiredValueInt)
{{
	this.{_info.Context.IsSynchronizedFieldName} = valueInt;
	this.{_info.Context.EventName}?.Invoke(this, value);
}}")),
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
$@"return this.{_info.Context.IsSynchronizedFieldName} == 1;")))
					.WithLeadingTrivia(_info.Context.IsSynchronizedPropertySummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				property = null;
			}

			return property;
		}
		public EventFieldDeclarationSyntax GetContextEvent()
		{
			EventFieldDeclarationSyntax @event;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				@event = SyntaxFactory.EventFieldDeclaration(
					SyntaxFactory.VariableDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<EventHandler<Boolean>>())))
					.AddDeclarationVariables(
						SyntaxFactory.VariableDeclarator(_info.Context.EventName))
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.WithLeadingTrivia(_info.Context.EventSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				@event = null;
			}

			return @event;
		}
		public PropertyDeclarationSyntax GetContextSyncRootProperty()
		{
			PropertyDeclarationSyntax property;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Object>()),
						_info.Context.SyncRootPropertyName)
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration))
					.AddModifiers(
						SyntaxFactory.Token(
							_info.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword))
					.WithLeadingTrivia(_info.Context.SyncRootSummary.Split('\n').Select(SyntaxFactory.Comment))
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
		public MethodDeclarationSyntax GetContextInvokeMethod()
		{
			MethodDeclarationSyntax method;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.InvokeMethodName)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.InvokeMethodMethodParameterName))
					.WithType(
						SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action<Boolean>>())))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if({_info.Context.InvokeMethodMethodParameterName} != null)
{{
	lock({_info.Context.SyncRootPropertyName})
	{{
		{_info.Context.InvokeMethodMethodParameterName}.Invoke({_info.Context.IsSynchronizedPropertyName});
	}}
}}"))
				.WithLeadingTrivia(_info.Context.InvokeMethodSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		public MethodDeclarationSyntax GetContextDesynchronizeMethod()
		{
			MethodDeclarationSyntax method;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.DesynchronizeMethodName)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if (this.{_info.Context.IsSynchronizedPropertyName})
{{
	lock ({_info.Context.SyncRootPropertyName})
	{{
		if (this.{_info.Context.IsSynchronizedPropertyName})
		{{
			var {_info.Context.LocalAuthorityName} = {_info.Context.AuthorityPropertyName};
			if (authority != null)
			{{
				var {_info.Context.TypeIdLocalName} = {GetTypeIdPropertyAccess(true)};
				var {_info.Context.SourceInstanceIdLocalName} = {GetSourceInstanceIdPropertyAccess(true)};
				var {_info.Context.InstanceIdLocalName} = {GetInstanceIdPropertyAccess(true)};

				this.{_info.Context.DesynchronizeUnlockedMethodName}({_info.Context.TypeIdLocalName}, {_info.Context.SourceInstanceIdLocalName}, {_info.Context.InstanceIdLocalName}, null);
			}}

			{_info.Context.IsSynchronizedPropertyName} = false;
		}}
	}}
}}"))
				.WithLeadingTrivia(_info.Context.DesynchronizeMethodSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		public MethodDeclarationSyntax GetContextDesynchronizeUnlockedMethod()
		{
			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.DesynchronizeUnlockedMethodName)
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.TypeIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.SourceInstanceIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.InstanceIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.LocalAuthorityName))
					.WithType(
						_info.ISynchronizationAuthorityIdentifier.AsSyntax()),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.OnRevertLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())))
				.AddModifiers(
					SyntaxFactory.Token(
						SyntaxKind.ProtectedKeyword),
					SyntaxFactory.Token(
						_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
						SyntaxKind.VirtualKeyword :
						SyntaxKind.OverrideKeyword));

			method = method
				.AddBodyStatements(_info.Context.RevertableUnsubscriptions)
				.WithLeadingTrivia(_info.Context.DesynchronizeUnlockedMethodSummary.Split('\n').Select(SyntaxFactory.Comment));

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
			{
				method = method
					.AddBodyStatements(
						SyntaxFactory.ParseStatement($"base.{_info.Context.DesynchronizeUnlockedMethodName}(" +
													 $"{_info.Context.TypeIdLocalName}, " +
													 $"{_info.Context.SourceInstanceIdLocalName}, " +
													 $"{_info.Context.InstanceIdLocalName}, " +
													 $"{_info.Context.OnRevertLocalName}: () => this.{_info.Context.SynchronizeUnlockedMethodName}(" +
													 $"{_info.Context.TypeIdLocalName}, " +
													 $"{_info.Context.SourceInstanceIdLocalName}, " +
													 $"{_info.Context.InstanceIdLocalName}, " +
													 $"{_info.Context.OnRevertLocalName}: null));"));
			}

			return method;
		}
		public MethodDeclarationSyntax GetContextSynchronizeMethod()
		{
			MethodDeclarationSyntax method;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.SynchronizeMethodName)
				.AddModifiers(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword))
				.AddBodyStatements(
					SyntaxFactory.ParseStatement(
$@"if (!this.{_info.Context.IsSynchronizedPropertyName})
{{
	lock ({_info.Context.SyncRootPropertyName})
	{{
		if (!this.{_info.Context.IsSynchronizedPropertyName})
		{{
			var authority = {_info.Context.AuthorityPropertyName};
			if (authority != null)
			{{
				var typeId = {_info.Context.TypeIdPropertyName};
				var sourceInstanceId = {_info.Context.SourceIdPropertyName};
				var instanceId = {_info.Context.InstancePropertyName};

				this.{_info.Context.SynchronizeUnlockedMethodName}(typeId, sourceInstanceId, instanceId, null);
			}}

			{_info.Context.IsSynchronizedPropertyName} = true;
		}}
	}}
}}"))
				.WithLeadingTrivia(_info.Context.SynchronizeMethodSummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else
			{
				method = null;
			}

			return method;
		}
		public MethodDeclarationSyntax GetContextSynchronizeUnlockedMethod()
		{
			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.SynchronizeUnlockedMethodName)
				.AddParameterListParameters(
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.TypeIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.SourceInstanceIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.InstanceIdLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<String>())),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.LocalAuthorityName))
					.WithType(
						_info.ISynchronizationAuthorityIdentifier.AsSyntax()),
					SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(_info.Context.OnRevertLocalName))
					.WithType(SyntaxFactory.ParseTypeName(TypeIdentifier.Create<Action>())))
				.AddModifiers(
					SyntaxFactory.Token(
						SyntaxKind.ProtectedKeyword),
					SyntaxFactory.Token(
						_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
						SyntaxKind.VirtualKeyword :
						SyntaxKind.OverrideKeyword));

			method = method
				.AddBodyStatements(_info.Context.RevertableSubscriptions)
				.WithLeadingTrivia(_info.Context.SynchronizeUnlockedMethodSummary.Split('\n').Select(SyntaxFactory.Comment));

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
			{
				method = method
					.AddBodyStatements(
						SyntaxFactory.ParseStatement($"base.{_info.Context.SynchronizeUnlockedMethodName}(" +
													 $"{_info.Context.TypeIdLocalName}, " +
													 $"{_info.Context.SourceInstanceIdLocalName}, " +
													 $"{_info.Context.InstanceIdLocalName}, " +
													 $"{_info.Context.OnRevertLocalName}: () => this.{_info.Context.DesynchronizeUnlockedMethodName}(" +
													 $"{_info.Context.TypeIdLocalName}, " +
													 $"{_info.Context.SourceInstanceIdLocalName}, " +
													 $"{_info.Context.InstanceIdLocalName}, " +
													 $"{_info.Context.OnRevertLocalName}: null));"));
			}

			return method;
		}
		public MethodDeclarationSyntax GetContextResynchronizeMethod()
		{
			//TODO: continue with Resynchronization / UnlockedResynchronization

			var authorityAccess = _info.Context.AuthorityPropertyName;
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);
			var unsubscriptions = _info.Context.RevertableUnsubscriptions;
			var subscriptions = _info.Context.RevertableSubscriptions;
			var pulls = _info.Context.Pulls;
			var pullAssignments = _info.Context.PullAssignments;

			var method = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.ParseTypeName("void"),
					_info.Context.ResynchronizeMethodName)
				.WithLeadingTrivia(_info.Context.ResynchronizeMethodSummary.Split('\n').Select(SyntaxFactory.Comment))
				.AddBodyStatements(
					SyntaxFactory.LockStatement(
						SyntaxFactory.ParseExpression($"this.{_info.Context.SyncRootPropertyName}"),
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
		public PropertyDeclarationSyntax GetContextAuthorityProperty()
		{
			PropertyDeclarationSyntax property;

			if (_info.Declared.Authority != null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						_info.ISynchronizationAuthorityIdentifier.AsSyntax(),
						_info.Context.AuthorityPropertyName)
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(
							SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
								$"return {_info.Context.InstancePropertyAccess}.{_info.Declared.Authority.Identifier};")))
					.AddModifiers(
						SyntaxFactory.Token(
							_info.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed &&
							_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword));

				if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName != null)
				{
					property = property
						.AddModifiers(
							SyntaxFactory.Token(
								SyntaxKind.OverrideKeyword));
				}

				property = property.WithLeadingTrivia(_info.Context.AuthorityPropertySummary.Split('\n').Select(SyntaxFactory.Comment));
			}
			else if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				throw new Exception($"Either {_info.Declared.TypeIdentifier} or one of its synchronized base classes must provide a property annotated with {_info.SynchronizationAuthorityAttributeIdentifier}.");
			}
			else
			{
				property = null;
			}

			return property;
		}
		public PropertyDeclarationSyntax GetContextTypeIdProperty()
		{
			PropertyDeclarationSyntax property;

			var typeId = _info.Declared.TypeId;

			if (_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
			{
				if(typeId == null)
				{

				}

				property = SyntaxFactory.PropertyDeclaration(
						TypeIdentifier.Create<String>().AsSyntax(),
						_info.Context.TypeIdPropertyName)
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(
							SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
								$"return {_info.Context.InstancePropertyAccess}.{typeId.Identifier};")))
					.AddModifiers(
						SyntaxFactory.Token(
							_info.Declared.SynchronizationTargetAttribute.ContextTypeIsSealed &&
							_info.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null ?
							SyntaxKind.PrivateKeyword :
							SyntaxKind.ProtectedKeyword));
			}
			else if(typeId != null)
			{
				property = SyntaxFactory.PropertyDeclaration(
						TypeIdentifier.Create<String>().AsSyntax(),
						_info.Context.TypeIdPropertyName)
					.AddModifiers(
						SyntaxFactory.Token(
							SyntaxKind.OverrideKeyword)); ;
			}
			else
			{
				property = null;
			}


			property = property?.WithLeadingTrivia(_info.Context.TypeIdPropertySummary.Split('\n').Select(SyntaxFactory.Comment));

			return property;
		}
		public PropertyDeclarationSyntax GetContextSourceInstanceIdProperty()
		{
			return null;
		}
		public PropertyDeclarationSyntax GetContextInstanceIdProperty()
		{
			return null;
		}

		public StatementSyntax GetRevertableUnsubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
		{
			requiredRevertions.Add(field);

			var statement = SyntaxFactory.TryStatement()
				.AddBlockStatements(
					GetUnsubscription(field))
				.AddCatches(
					SyntaxFactory.CatchClause()
						.AddBlockStatements(
							requiredRevertions.Select(f => GetSubscription(f))
							.Append(SyntaxFactory.ParseStatement($"{_info.Context.OnRevertLocalName}?.Invoke();"))
							.Append(SyntaxFactory.ThrowStatement())
							.ToArray()));

			return statement;
		}
		public StatementSyntax GetUnsubscription(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);

			var statement = SyntaxFactory.ParseStatement($"{_info.Context.LocalAuthorityName}.Unsubscribe(" +
														 $"{_info.Context.TypeIdLocalName}, \"" +
														 $"{propertyName}\", " +
														 $"{_info.Context.SourceInstanceIdLocalName}, " +
														 $"{_info.Context.InstanceIdLocalName});");

			return statement;
		}

		public StatementSyntax GetRevertableSubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
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
							.Append(SyntaxFactory.ParseStatement($"{_info.Context.OnRevertLocalName}?.Invoke();"))
							.Append(SyntaxFactory.ThrowStatement())
							.ToArray()));

			return statement;
		}
		public StatementSyntax GetSubscription(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setBlock = GetSetBlock(field, fromWithinContext: true);

			var statement = SyntaxFactory.ParseStatement($"{_info.Context.LocalAuthorityName}.Subscribe<" +
														 $"{fieldType}>(" +
														 $"{_info.Context.TypeIdLocalName}, \"" +
														 $"{propertyName}\", " +
														 $"{_info.Context.SourceInstanceIdLocalName}, " +
														 $"{_info.Context.InstanceIdLocalName}, (value) => {{" +
														 $"{setBlock}}});");

			return statement;
		}

		public StatementSyntax GetPull(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);

			var statement = SyntaxFactory.ParseStatement($"var {_info.Context.PullValuePrefix}{propertyName} = " +
														 $"{_info.Context.LocalAuthorityName}.Pull<" +
														 $"{fieldType}>(" +
														 $"{_info.Context.TypeIdLocalName}, \"" +
														 $"{propertyName}\", " +
														 $"{_info.Context.SourceInstanceIdLocalName}, " +
														 $"{_info.Context.InstanceIdLocalName});");

			return statement;
		}
		public StatementSyntax GetPullAssignment(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);
			var fieldName = GetFieldName(field);

			var statement = SyntaxFactory.ParseStatement($"{_info.Context.InstancePropertyAccess}." +
														 $"{fieldName} = " +
														 $"{_info.Context.PullValuePrefix}" +
														 $"{propertyName};");

			return statement;
		}
		#endregion

		#region Misc
		public String GetSetBlock(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
			var instance = fromWithinContext ?
				$"{_info.Context.AuthorityPropertyName}." :
				"this.";
			var fieldName = GetFieldName(field);
			var propertyChangedCall = GetPropertyChangedCall(field, fromWithinContext);

			var del =
$@"{propertyChangingCall}
{instance}{fieldName} = value;{propertyChangedCall}";

			return del;
		}
		public String GetPropertyChangingCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{_info.Context.InstancePropertyAccess}." :
				String.Empty;
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: _info.SynchronizedAttributeFactory.TryBuild(a, _info.Declared.SemanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}{_info.Members.PropertyChangingEventMethodName}(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		public String GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{_info.Context.InstancePropertyAccess}." :
				String.Empty;
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: _info.SynchronizedAttributeFactory.TryBuild(a, _info.Declared.SemanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}{_info.Members.PropertyChangedEventMethodName}(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		public String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		public TypeIdentifier GetFieldType(FieldDeclarationSyntax field)
		{
			var type = field.Declaration.Type;
			var symbol = _info.Declared.SemanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? _info.Declared.SemanticModel.GetTypeInfo(type).Type;

			var identifier = TypeIdentifier.Create(symbol);

			return identifier;
		}
		public String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			var attributeInstance = field.AttributeLists.SelectMany(al => al.Attributes)
				.Select(a => (success: _info.SynchronizedAttributeFactory.TryBuild(a, _info.Declared.SemanticModel, out var instance), instance))
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
					propertyName = getPrefixedName(_info.Members.ObservablePropertyPrefix);
				}
				else
				{
					propertyName = getPrefixedName(_info.Members.SynchronizedPropertyPrefix);
				}

				String getPrefixedName(String prefix)
				{
					return String.Concat(prefix, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
				}
			}

			return propertyName;
		}
		public Boolean TryGetNamespace(SyntaxNode node, out BaseNamespaceDeclarationSyntax namespaceDeclaration)
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

		public void ThrowIfMultiple<T>(T[] items, String declarationType, TypeIdentifier attribute)
		{
			if (items.Length > 1)
			{
				throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {_info.Declared.TypeIdentifier}.");
			}
		}
		#endregion
	}
}
