using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace TestGenerator
{
	internal sealed class ObjectSyncSourceExtractor
	{
		private static Namespace AttributesNamespace = Namespace.Create<ObjectSync.Attributes.SynchronizedAttribute>();

		private static TypeIdentifierName SynchronizedAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedAttribute>();
		private static TypeIdentifierName SynchronizationIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationIdAttribute>();
		private static TypeIdentifierName SynchronizationInstanceIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationInstanceIdAttribute>();
		private static TypeIdentifierName SynchronizationAuthorityAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationAuthorityAttribute>();
		private static TypeIdentifierName GenerateEventsAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.GenerateEventsAttribute>();

		private static TypeIdentifier SynchronizedAttributeIdentifier = TypeIdentifier.Create(SynchronizedAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationIdAttributeIdentifier = TypeIdentifier.Create(SynchronizationIdAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationInstanceIdAttributeIdentifier = TypeIdentifier.Create(SynchronizationInstanceIdAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier = TypeIdentifier.Create(SynchronizationAuthorityAttributeName, AttributesNamespace);
		private static TypeIdentifier GenerateEventsAttributeIdentifier = TypeIdentifier.Create(GenerateEventsAttributeName, AttributesNamespace);

		private const String SYNCHRONIZATION_STATE_NAME = "SynchronizationState";
		private const String SYNCHRONIZATION_ID_NAME = "SynchronizationId";
		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";
		private const String SYNCHRONIZATION_INSTANCE_ID_FALLBACK_NAME = "InstanceId";
		private const String FORMAT_ITEM = "{" + nameof(FORMAT_ITEM) + "}";

		private readonly CompilationAnalyzer _analyzer;
		private readonly Compilation _compilation;

		public ObjectSyncSourceExtractor(Compilation compilation)
		{
			_analyzer = new CompilationAnalyzer(compilation);
			_compilation = compilation;
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
			var synchronizationIdDeclaration = GetSynchronizationIdPropertyDeclaration(typeDeclaration);
			var instanceIdDeclaration = GetInstanceIdPropertyDeclaration(typeDeclaration);
			var synchronizationStateDeclaration = GetSynchronizationStateDeclaration(typeDeclaration);

			var bodyParts = propertyDeclarations
				.Append(eventMethods)
				.Append(synchronizationIdDeclaration)
				.Append(instanceIdDeclaration)
				.Append(synchronizationStateDeclaration)
				.Where(s => !String.IsNullOrWhiteSpace(s));

			var body = String.Join("\n\n", bodyParts);

			var declaration = template.Replace(FORMAT_ITEM, body);

			return declaration;
		}

		private String GetSynchronizationStateDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = GetName(typeDeclaration);
			var synchronizeMethod = GetSynchronizeMethod(typeDeclaration);
			var desynchronizMethod = GetDesynchronizeMethod(typeDeclaration);

			var declaration =
$@"private sealed class SynchronizationState
{{
public SynchronizationState({name} instance)
{{
_instance = instance;
}}

public Boolean IsSynchronized {{get; private set;}}

private readonly {name} _instance; 
private readonly System.Threading.SemaphoreSlim _gate = new System.Threading.SemaphoreSlim(1, 1);

{synchronizeMethod}
{desynchronizMethod}
}}

private SynchronizationState __synchronizationState;
private SynchronizationState GetSynchronizationState()
{{
return __synchronizationState ??= new SynchronizationState(this);
}}";

			return declaration;

		}

		private String GetEventMethodDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var methods = _analyzer.GetFieldDeclarations(typeDeclaration)
				.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, GenerateEventsAttributeIdentifier)) ?
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
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var unsubscriptions = String.Join("\n", GetUnsubscriptionExpressions(typeDeclaration));

			var declaration =
$@"public void Desynchronize()
{{
_gate.Wait();
try
{{
var authority = _instance.{authority};
if(authority != null)
{{
{unsubscriptions}

IsSynchronized = false;
}}
}}finally{{
_gate.Release();
}}
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
			var propertyName = GetGeneratedPropertyName(field);
			var idName = GetSynchronizationIdPropertyName(typeDeclaration);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);
			var expression =
$"authority.Unsubscribe(_instance.{idName}, \"{propertyName}\", _instance.{instanceIdName});";

			return expression;
		}

		private String GetSynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var subscriptions = String.Join("\n", GetSubscriptionExpressions(typeDeclaration));
			var pulls = String.Join("\n", GetPullExpressions(typeDeclaration));

			var initializer =
$@"public void Synchronize()
{{
_gate.Wait();
try
{{
var authority = _instance.{authority};
if(authority != null)
{{
{pulls}

{subscriptions}

IsSynchronized = true;
}}
}}finally{{
_gate.Release();
}}
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
			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);
			var idPropertyName = GetSynchronizationIdPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);

			var subscription =
$"_instance.{fieldName} = authority.Pull<{fieldType}>(_instance.{idPropertyName}, \"{propertyName}\", _instance.{instanceIdName});";

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
			var fieldType = GetFieldType(field);
			var idPropertyName = GetSynchronizationIdPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var instanceIdName = GetInstanceIdPropertyName(typeDeclaration);

			var subscription =
$"authority.Subscribe<{fieldType}>(_instance.{idPropertyName}, \"{propertyName}\", _instance.{instanceIdName}, _instance.Set{propertyName});";

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
SetAndPush{propertyName}(value);
}}
}}

private void Set{propertyName}({fieldType} value)
{{{propertyChangingCall}
{fieldName} = value;{propertyChangedCall}
}}

private void SetAndPush{propertyName}({fieldType} value)
{{
Set{propertyName}(value);
{authorityName}.Push<{fieldType}>({idPropertyName}, ""{propertyName}"", {instanceIdName}, value);
}}";

			return property;
		}

		private String GetPropertyChangingCall(FieldDeclarationSyntax field)
		{
			var call = _analyzer.HasAttribute(field.AttributeLists, field, GenerateEventsAttributeIdentifier) ?
				$"\nOnPropertyChanging(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field)
		{
			var call = _analyzer.HasAttribute(field.AttributeLists, field, GenerateEventsAttributeIdentifier) ?
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
				if (children[0] is LiteralExpressionSyntax literalExpression &&
					literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
				{
					propertyName = literalExpression.Token.ValueText;
				}
				else if (children[0] is InvocationExpressionSyntax invocation &&
					invocation.Expression is IdentifierNameSyntax identifierName &&
					identifierName.Identifier.ValueText == "nameof")
				{
					propertyName = invocation.ArgumentList.Arguments.SingleOrDefault()?.GetText().ToString();
				}else if(children[0] is IdentifierNameSyntax identifier &&
					_compilation.GetSemanticModel(identifier.SyntaxTree).GetSymbolInfo(identifier).Symbol is IFieldSymbol constantValue)
				{
					propertyName = constantValue.ConstantValue?.ToString();
				}
			}

			if (String.IsNullOrEmpty(propertyName))
			{
				var fieldName = GetFieldName(field);

				if (fieldName[0] == '_' || fieldName[0] == Char.ToLowerInvariant(fieldName[0]))
				{
					var sanitizedFieldName = Regex.Replace(fieldName, @"^_*", String.Empty);
					propertyName = String.Concat(Char.ToUpperInvariant(sanitizedFieldName[0]), sanitizedFieldName.Substring(1, sanitizedFieldName.Length - 1));

				}
				else
				{
					propertyName = String.Concat(SYNCHRONIZED_PROPERTY_PREFIX, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
				}

			}

			return propertyName;
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
			var name = TryGetSynchronizationIdProperty(typeDeclaration, out var property) ? property.Identifier.Text : SYNCHRONIZATION_ID_NAME;

			return name;
		}
		private String GetSynchronizationIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var declaration = TryGetSynchronizationIdProperty(typeDeclaration, out var property) ?
				String.Empty :
				$"private System.String {SYNCHRONIZATION_ID_NAME} {{get; set;}} = System.Guid.NewGuid().ToString();";

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
