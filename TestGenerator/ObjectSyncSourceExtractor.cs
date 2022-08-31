using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestGenerator
{
	internal sealed class ObjectSyncSourceExtractor
	{
		private static Namespace AttributesNamespace = Namespace.Create<ObjectSync.Attributes.SynchronizedAttribute>();

		private static TypeIdentifierName SynchronizedAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedAttribute>();
		private static TypeIdentifierName SynchronizedFlagAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedFlagAttribute>();
		private static TypeIdentifierName SynchronizationIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationIdAttribute>();
		private static TypeIdentifierName SynchronizationAuthorityAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationAuthorityAttribute>();

		private static TypeIdentifier SynchronizedAttributeIdentifier = TypeIdentifier.Create(SynchronizedAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizedFlagAttributeIdentifier = TypeIdentifier.Create(SynchronizedFlagAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationIdAttributeIdentifier = TypeIdentifier.Create(SynchronizationIdAttributeName, AttributesNamespace);
		private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier = TypeIdentifier.Create(SynchronizationAuthorityAttributeName, AttributesNamespace);

		private const String SYNCHRONIZATION_FLAG_FALLBACK_NAME = "IsSynchronized";
		private const String SYNCHRONIZATION_ID_FALLBACK_NAME = "SynchronizationId";
		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";
		private const String FORMAT_ITEM = "{" + nameof(FORMAT_ITEM) + "}";

		private readonly CompilationAnalyzer _analyzer;

		public ObjectSyncSourceExtractor(Compilation compilation)
		{
			_analyzer = new CompilationAnalyzer(compilation);
		}

		public IEnumerable<GeneratedSource> GetSources()
		{
			var syncDeclarations = GetSyncDeclarations();
			var sources = syncDeclarations.Select(GetGeneratedSource);

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
			var source = GetGeneratedTypeDeclaration(typeDeclaration);
			var objectSyncSource = new GeneratedSource(source, className);

			return objectSyncSource;
		}

		private String GetGeneratedTypeDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			String template = GetClassTemplate(typeDeclaration);

			var propertyDeclarations = String.Join("\n\n", GetGeneratedPropertyDeclarations(typeDeclaration));
			var methodDeclarations = String.Join("\n\n", GetPartialMethodDeclarations());
			var flagDeclaration = GetFlagPropertyDeclaration(typeDeclaration);
			var idDeclaration = GetIdPropertyDeclaration(typeDeclaration);
			var initializerDeclaration = GetInitializerDeclaration(typeDeclaration);
			var disposeDeclaration = GetDisposeDeclaration(typeDeclaration);

			var body =
$@"{propertyDeclarations}
{methodDeclarations}{flagDeclaration}{idDeclaration}
{initializerDeclaration}
{disposeDeclaration}";

			var declaration = template.Replace(FORMAT_ITEM, body);

			return declaration;
		}

		private String GetDisposeDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var unsubscriptions = String.Join("\n", GetUnsubscriptionExpressions(typeDeclaration));
			var flagName = GetFlagPropertyName(typeDeclaration);

			var declaration =
$@"private System.Boolean disposedValue = false;

private void Dispose(Boolean disposing)
{{
	if (!disposedValue)
	{{
		if (disposing)
		{{
			DisposeManagedResources();
		}}
		
		{unsubscriptions}

		{flagName} = false;

		DisposeUnmanagedResources();
		disposedValue = true;
	}}
}}

~MySynchronizedObject()
{{
	Dispose(disposing: false);
}}

public void Dispose()
{{
	Dispose(disposing: true);
	GC.SuppressFinalize(this);
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
			var idName = GetIdPropertyName(typeDeclaration);
			var expression =
$"{authority}.Unsubscribe({idName}, nameof{propertyName}))";

			return expression;
		}

		private String GetInitializerDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var subscriptions = String.Join("\n", GetSubscriptionExpressions(typeDeclaration));
			var flagName = GetFlagPropertyName(typeDeclaration);

			var initializer =
$@"private void Synchronize()
{{
{subscriptions}
{flagName} = true;
}}";

			return initializer;
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
			var fieldName = GetFieldName(field);
			var idPropertyName = GetIdPropertyName(typeDeclaration);
			var propertyName = GetGeneratedPropertyName(field);
			var subscription =
$@"{authority}.Subscribe<{fieldType}>({idPropertyName}, nameof({propertyName}), (value) => {fieldName} = value);";

			moreturn subscription;
		}

		private IEnumerable<String> GetPartialMethodDeclarations()
		{
			var methods = new[]
			{
				"partial void OnPropertyChanging(System.ComponentModel.PropertyChangingEventArgs args);",
				"partial void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args);" ,
				"partial void DisposeManagedResources();",
				"partial void DisposeUnmanagedResources();"
			};

			return methods;
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
			var authorityName = GetAuthorityPropertyName(typeDeclaration);
			var idPropertyName = GetIdPropertyName(typeDeclaration);

			var property =
$@"public {fieldType} {propertyName} 
		        {{
		            get
		            {{
		                return {fieldName};
		            }}
		            set
		            {{
		                {fieldName} = value;
		                {authorityName}.Push<{fieldType}>({idPropertyName}, nameof({propertyName}), value);
		            }}
		        }}";

			return property;
		}
		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			_analyzer.TryGetAttributes(field.AttributeLists, field, SynchronizedAttributeIdentifier, out var attributes);

			var propertyNameArgument = attributes.Single()
				.ArgumentList?
				.Arguments
				.Single();

			if (propertyNameArgument != null && !propertyNameArgument.IsKind(SyntaxKind.StringLiteralExpression))
			{
				//
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
			var declaration = TryGetFlagProperty(typeDeclaration, out var flagProperty) ? String.Empty : $"\n\npublic System.Boolean {SYNCHRONIZATION_FLAG_FALLBACK_NAME} {{get; private set;}}";

			return declaration;
		}

		private Boolean TryGetIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			var properties = _analyzer.GetPropertyDeclarations(typeDeclaration, new[] { SynchronizationIdAttributeIdentifier });
			ThrowIfMultiple(properties, "properties", SynchronizationIdAttributeIdentifier, typeDeclaration);

			idProperty = properties.SingleOrDefault();

			return idProperty != null;
		}
		private String GetIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = TryGetIdProperty(typeDeclaration, out var property) ? property.Identifier.Text : SYNCHRONIZATION_ID_FALLBACK_NAME;

			return name;
		}
		private String GetIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var declaration = TryGetIdProperty(typeDeclaration, out var property) ? String.Empty : $"\n\nprivate System.String {SYNCHRONIZATION_ID_FALLBACK_NAME} {{get;}} = System.Guid.NewGuid().ToString();";

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
