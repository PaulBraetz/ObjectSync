using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TestGenerator
{
	internal sealed class ObjectSyncSourceExtractor
	{
		private static Namespace AttributesNamespace = Namespace.Create<ObjectSync.Attributes.SynchronizedAttribute>();

		private static TypeIdentifierName SynchronizedAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedAttribute>();
		private static TypeIdentifierName SynchronizedFlagAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedFlagAttribute>();
		private static TypeIdentifierName SynchronizationIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationIdAttribute>();
		private static TypeIdentifierName SynchronizationInstanceIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationInstanceIdAttribute>();
		private static TypeIdentifierName SynchronizationAuthorityAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationAuthorityAttribute>();
		private static TypeIdentifierName EventIntegrationAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.EventIntegrationAttribute>();

		private static TypeIdentifier SynchronizedAttributeIdentifier = TypeIdentifier.Create(SynchronizedAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizedFlagAttributeIdentifier = TypeIdentifier.Create(SynchronizedFlagAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationIdAttributeIdentifier = TypeIdentifier.Create(SynchronizationIdAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationInstanceIdAttributeIdentifier = TypeIdentifier.Create(SynchronizationInstanceIdAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier = TypeIdentifier.Create(SynchronizationAuthorityAttributeName, AttributesNamespace);
		private static TypeIdentifier EventIntegrationAttributeIdentifier = TypeIdentifier.Create(EventIntegrationAttributeName, AttributesNamespace);

		private const String SYNCHRONIZATION_FLAG_FALLBACK_NAME = "IsSynchronized";
		private const String SYNCHRONIZATION_ID_FALLBACK_NAME = "SynchronizationId";
		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";
		private const String SYNCHRONIZATION_INSTANCE_ID_FALLBACK_NAME = "InstanceId";
		private const String FORMAT_ITEM = "{" + nameof(FORMAT_ITEM) + "}";

		private readonly CompilationAnalyzer _analyzer;

		public ObjectSyncSourceExtractor(Compilation compilation)
		{
			_analyzer = new CompilationAnalyzer(compilation);
		}

		public IEnumerable<GeneratedSource> GetSources()
		{
			var syncDeclarations = GetSyncDeclarations();
			var sources = syncDeclarations.Select(GetGeneratedSource).ToArray();

			return sources;
		}

		private IEnumerable<BaseTypeDeclarationSyntax> GetSyncDeclarations()
		{
			var syncDeclarations = _analyzer.GetTypeDeclarations()
				.Where(td => _analyzer.GetFieldDeclarations(td, include: new[] { SynchronizedAttributeIdentifier }).Any());

			return syncDeclarations;
		}

		private GeneratedSource GetGeneratedSource(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var className = GetName(typeDeclaration);
			String source = null;
			try
			{
				source = GetGeneratedTypeDeclaration(typeDeclaration);
			}
			catch (Exception ex)
			{
				source = GetError(typeDeclaration, ex);
			}
			var objectSyncSource = new GeneratedSource(source, className);
			return objectSyncSource;
		}

		private String GetError(BaseTypeDeclarationSyntax typeDeclaration, Exception ex)
		{
			var error =
$@"/*
An error occured while generating this source file for {GetName(typeDeclaration)}:
{ex}
*/";

			return error;
		}

		private String GetGeneratedTypeDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			String template = GetClassTemplate(typeDeclaration);

			var propertyDeclarations = GetGeneratedPropertyDeclarations(typeDeclaration);
			var eventMethods = GetEventMethodDeclarations(typeDeclaration);
			var flagDeclaration = GetFlagPropertyDeclaration(typeDeclaration);
			var synchronizationIdDeclaration = GetSynchronizationIdPropertyDeclaration(typeDeclaration);
			var instanceIdDeclaration = GetInstanceIdPropertyDeclaration(typeDeclaration);
			var synchronizeMethod = GetSynchronizeMethod(typeDeclaration);
			var desynchronizMethod = GetDesynchronizeMethod(typeDeclaration);

			var bodyParts = propertyDeclarations
				.Append(eventMethods)
				.Append(flagDeclaration)
				.Append(synchronizationIdDeclaration)
				.Append(instanceIdDeclaration)
				.Append(synchronizeMethod)
				.Append(desynchronizMethod)
				.Where(s => !String.IsNullOrWhiteSpace(s));

			var body = String.Join("\n\n", bodyParts);

			var declaration = template.Replace(FORMAT_ITEM, body);

			return declaration;
		}

		private String GetEventMethodDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var methods = _analyzer.GetFieldDeclarations(typeDeclaration)
				.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, EventIntegrationAttributeIdentifier)) ?
@"partial void OnPropertyChanging(System.ComponentModel.PropertyChangingEventArgs args);

private void OnPropertyChanging(System.String propertyName)
{
OnPropertyChanging(new System.ComponentModel.PropertyChangingEventArgs(propertyName));
}

partial void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args);

private void OnPropertyChanged(System.String propertyName)
{
OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}" :
String.Empty;

			return methods;
		}

		private String GetDesynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var unsubscriptions = String.Join("\n", GetUnsubscriptionExpressions(typeDeclaration));
			var flagName = GetFlagPropertyName(typeDeclaration);

			var declaration =
$@"private void Desynchronize()
{{
{unsubscriptions}

{flagName} = false;
}}";

			return declaration;
		}
		private IEnumerable<String> GetUnsubscriptionExpressions(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var expressions = fields.Select(f => GetUnsubscriptionExpression(f, typeDeclaration));

			return expressions;
		}
		private String GetUnsubscriptionExpression(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var idName = GetSynchronizationIdPropertyName(typeDeclaration);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);
			var expression =
$"{authority}?.Unsubscribe({idName}, \"{propertyName}\", {instanceIdName});";

			return expression;
		}

		private String GetSynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var subscriptions = String.Join("\n", GetSubscriptionExpressions(typeDeclaration));
			var pulls = String.Join("\n", GetPullExpressions(typeDeclaration));
			var flagName = GetFlagPropertyName(typeDeclaration);

			var initializer =
$@"private void Synchronize()
{{
{pulls}

{subscriptions}

{flagName} = true;
}}";

			return initializer;
		}

		private IEnumerable<String> GetPullExpressions(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var pulls = fields.Select(f => GetPullExpression(f, typeDeclaration));

			return pulls;
		}
		private String GetPullExpression(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);
			var idPropertyName = GetSynchronizationIdPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);

			var subscription =
$"{fieldName} = {authority}.Pull<{fieldType}>({idPropertyName}, \"{propertyName}\", {instanceIdName});";

			return subscription;
		}

		private IEnumerable<String> GetSubscriptionExpressions(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var subscriptions = fields.Select(f => GetSubscriptionExpression(f, typeDeclaration));

			return subscriptions;
		}
		private String GetSubscriptionExpression(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var fieldType = GetFieldType(field);
			var idPropertyName = GetSynchronizationIdPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);

			var subscription =
$"{authority}.Subscribe<{fieldType}>({idPropertyName}, \"{propertyName}\", {instanceIdName}, UnsynchronizedSet{propertyName});";

			return subscription;
		}

		private IEnumerable<FieldDeclarationSyntax> GetFields(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return _analyzer.GetFieldDeclarations(typeDeclaration, new[] { SynchronizedAttributeIdentifier });
		}
		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private String GetFieldType(FieldDeclarationSyntax field)
		{
			return _analyzer.GetTypeIdentifier(field.Declaration.Type).ToString();
		}

		private IEnumerable<String> GetGeneratedPropertyDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var properties = fields.Select(f => GetGeneratedPropertyDeclaration(f, typeDeclaration));

			return properties;
		}
		private String GetGeneratedPropertyDeclaration(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);
			var fieldName = GetFieldName(field);
			var propertyChangingCall = GetPropertyChangingCall(field);
			var propertyChangedCall = GetPropertyChangedCall(field);
			var authorityName = GetAuthorityPropertyName(typeDeclaration);
			var idPropertyName = GetSynchronizationIdPropertyName(typeDeclaration);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);

			var property =
$@"public {fieldType} {propertyName} 
{{
get
{{
return {fieldName};
}}
set
{{
SynchronizedSet{propertyName}(value);
}}
}}

private void UnsynchronizedSet{propertyName}({fieldType} value)
{{{propertyChangingCall}
{fieldName} = value;{propertyChangedCall}
}}

private void SynchronizedSet{propertyName}({fieldType} value)
{{
UnsynchronizedSet{propertyName}(value);
{authorityName}.Push<{fieldType}>({idPropertyName}, ""{propertyName}"", {instanceIdName}, value);
}}";

			return property;
		}

		private String GetPropertyChangingCall(FieldDeclarationSyntax field)
		{
			var call = _analyzer.HasAttribute(field.AttributeLists, field, EventIntegrationAttributeIdentifier) ?
				$"\nOnPropertyChanging(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field)
		{
			var call = _analyzer.HasAttribute(field.AttributeLists, field, EventIntegrationAttributeIdentifier) ?
				$"\nOnPropertyChanged(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			_analyzer.TryGetAttributes(field.AttributeLists, field, SynchronizedAttributeIdentifier, out var attributes);

			var propertyNameArgument = attributes.Single()
				.ArgumentList?
				.Arguments
				.Single();

			String propertyName = String.Empty;

			if (propertyNameArgument != null)
			{
				var children = propertyNameArgument.ChildNodes().ToArray();
				if (children.First() is LiteralExpressionSyntax literalExpression &&
					literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
				{
					propertyName = literalExpression.Token.ValueText;
				}
				else if (children.First() is InvocationExpressionSyntax invocation &&
					invocation.Expression is IdentifierNameSyntax identifierName &&
					identifierName.Identifier.ValueText == "nameof")
				{
					propertyName = invocation.ArgumentList.Arguments.SingleOrDefault()?.GetText().ToString();
				}
				else
				{
					throw new Exception($"Unable to generate property name for {GetFieldName(field)}.");
				}
			}

			if (String.IsNullOrEmpty(propertyName))
			{
				var fieldName = field.Declaration.Variables.Single().Identifier.Text;
				propertyName = fieldName.StartsWith("_") ?
					$"{SYNCHRONIZED_PROPERTY_PREFIX}{fieldName}" :
					$"{SYNCHRONIZED_PROPERTY_PREFIX}_{fieldName}";
			}

			return propertyName;
		}

		private Boolean TryGetFlagProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax flagProperty)
		{
			var properties = _analyzer.GetPropertyDeclarations(typeDeclaration, new[] { SynchronizedFlagAttributeIdentifier });
			if (properties.Count() > 1)
			{
				throw new Exception($"Multiple properties annotated with {SynchronizedFlagAttributeIdentifier} have been declared in {typeDeclaration.Identifier.Text}.");
			}
			flagProperty = properties.SingleOrDefault();

			return flagProperty != null;
		}
		private String GetFlagPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = TryGetFlagProperty(typeDeclaration, out var flagProperty) ?
				flagProperty.Identifier.Text :
				SYNCHRONIZATION_FLAG_FALLBACK_NAME;

			return name;
		}
		private String GetFlagPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var declaration = TryGetFlagProperty(typeDeclaration, out var flagProperty) ?
				String.Empty :
				$"\nprivate System.Boolean {SYNCHRONIZATION_FLAG_FALLBACK_NAME} {{get; set;}}";

			return declaration;
		}

		private Boolean TryGetSynchronizationIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			var properties = _analyzer.GetPropertyDeclarations(typeDeclaration, new[] { SynchronizationIdAttributeIdentifier });
			ThrowIfMultiple(properties, "properties", SynchronizationIdAttributeIdentifier, typeDeclaration);

			idProperty = properties.SingleOrDefault();

			return idProperty != null;
		}
		private String GetSynchronizationIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = TryGetSynchronizationIdProperty(typeDeclaration, out var property) ? property.Identifier.Text : SYNCHRONIZATION_ID_FALLBACK_NAME;

			return name;
		}
		private String GetSynchronizationIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var declaration = TryGetSynchronizationIdProperty(typeDeclaration, out var property) ?
				String.Empty :
				$"private System.String {SYNCHRONIZATION_ID_FALLBACK_NAME} {{get; set;}} = System.Guid.NewGuid().ToString();";

			return declaration;
		}

		private Boolean TryGetInstanceIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			var properties = _analyzer.GetPropertyDeclarations(typeDeclaration, new[] { SynchronizationInstanceIdAttributeIdentifier });
			ThrowIfMultiple(properties, "properties", SynchronizationInstanceIdAttributeIdentifier, typeDeclaration);

			idProperty = properties.SingleOrDefault();

			return idProperty != null;
		}
		private String GetInstanceIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = TryGetInstanceIdProperty(typeDeclaration, out var property) ? property.Identifier.Text : SYNCHRONIZATION_INSTANCE_ID_FALLBACK_NAME;

			return name;
		}
		private String GetInstanceIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var declaration = TryGetInstanceIdProperty(typeDeclaration, out var property) ?
				String.Empty :
				$"private System.String {SYNCHRONIZATION_INSTANCE_ID_FALLBACK_NAME} {{get;}} = System.Guid.NewGuid().ToString();";

			return declaration;
		}

		private String GetAuthorityPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var identifier = _analyzer.GetTypeIdentifier(typeDeclaration);

			var authorityProperties = _analyzer.GetPropertyDeclarations(typeDeclaration, new[] { SynchronizationAuthorityAttributeIdentifier });
			ThrowIfMultiple(authorityProperties, "properties", SynchronizationAuthorityAttributeIdentifier, typeDeclaration);

			var authorityProperty = authorityProperties.SingleOrDefault();
			if (authorityProperty == null)
			{
				throw new Exception($"A property annotated with {SynchronizationAuthorityAttributeIdentifier} must be declared in {identifier}.");
			}

			var authorityName = authorityProperty.Identifier.Text;

			return authorityName;
		}

		private String GetClassTemplate(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = GetName(typeDeclaration);
			var modifiers = GetModifiers(typeDeclaration);
			var template =
$@"{modifiers} class {name}
{{
{FORMAT_ITEM}
}}";
			SyntaxNode currentNode = typeDeclaration;
			while (currentNode.Parent != null)
			{
				if (typeDeclaration.Parent is BaseTypeDeclarationSyntax parentType)
				{
					var parentTemplate = GetClassTemplate(parentType);
					template = parentTemplate.Replace(FORMAT_ITEM, template);
					return template;
				}

				currentNode = currentNode.Parent;
			}

			var @namespace = GetNamespace(typeDeclaration);
			template =
$@"namespace {@namespace}
{{
{template}
}}";
			return template;
		}
		private String GetName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var className = typeDeclaration.Identifier.Text;

			return className;
		}
		private String GetNamespace(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var @namespace = _analyzer.GetTypeIdentifier(typeDeclaration).Namespace.ToString();

			return @namespace;
		}
		private String GetModifiers(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var modifiers = String.Join(" ", typeDeclaration.Modifiers);

			return modifiers;
		}

		private void ThrowIfMultiple<T>(IEnumerable<T> items, String declarationType, TypeIdentifier attribute, BaseTypeDeclarationSyntax typeDeclaration)
		{
			if (items.Count() > 1)
			{
				throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {_analyzer.GetTypeIdentifier(typeDeclaration)}.");
			}
		}
	}
}
