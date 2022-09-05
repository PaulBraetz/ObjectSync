using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ObjectSync.Generator
{
	internal sealed class ObjectSyncSourceExtractor
	{
		#region Statics
		private static readonly Namespace AttributesNamespace = Namespace.Create<ObjectSync.Attributes.SynchronizedAttribute>();

		private static readonly TypeIdentifierName TypeIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.TypeIdAttribute>();
		private static readonly TypeIdentifierName SourceInstanceIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SourceInstanceIdAttribute>();
		private static readonly TypeIdentifierName InstanceIdAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.InstanceIdAttribute>();
		private static readonly TypeIdentifier TypeIdAttributeIdentifier = TypeIdentifier.Create(TypeIdAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier InstanceIdAttributeIdentifier = TypeIdentifier.Create(InstanceIdAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier SourceInstanceIdAttributeIdentifier = TypeIdentifier.Create(SourceInstanceIdAttributeName, AttributesNamespace);

		private static readonly TypeIdentifierName SynchronizedAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizedAttribute>();
		private static readonly TypeIdentifierName SynchronizationAuthorityAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationAuthorityAttribute>();
		private static readonly TypeIdentifierName GenerateEventsAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.GenerateEventsAttribute>();
		private static readonly TypeIdentifier SynchronizedAttributeIdentifier = TypeIdentifier.Create(SynchronizedAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier SynchronizationAuthorityAttributeIdentifier = TypeIdentifier.Create(SynchronizationAuthorityAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier GenerateEventsAttributeIdentifier = TypeIdentifier.Create(GenerateEventsAttributeName, AttributesNamespace);
		#endregion

		#region Constants
		private const String TYPE_ID_DEFAULT_NAME = "TypeId";
		private const String SOURCE_INSTANCE_ID_DEFAULT_NAME = "SourceInstanceId";
		private const String INSTANCE_ID_DEFAULT_NAME = "InstanceId";

		private const Boolean TYPE_ID_DEFAULT_IS_STATIC = true;
		private const Boolean SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC = true;
		private const Boolean INSTANCE_ID_DEFAULT_IS_STATIC = false;

		private const String CONTEXT_INSTANCE_NAME = "_instance";
		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";
		private const String EVENTFUL_PROPERTY_PREFIX = "Eventful";
		private const String DEFAULT_PROPERTY_PREFIX = "Generated";
		private const String FORMAT_ITEM = "{" + nameof(FORMAT_ITEM) + "}";
		#endregion

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
			String source;
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
			var template = GetClassTemplate(typeDeclaration);

			var synchronizationContextDeclaration = GetSynchronizationContextDeclaration(typeDeclaration);
			var idDeclarations = GetIdDeclarations(typeDeclaration);
			var eventMethods = GetEventMethodDeclarations(typeDeclaration);

			var propertyDeclarations = GetGeneratedPropertyDeclarations(typeDeclaration);

			var bodyParts = new List<String>()
				.Append(synchronizationContextDeclaration)
				.Append(idDeclarations)
				.Append(eventMethods)
				.Concat(propertyDeclarations)
				.Where(s => !String.IsNullOrEmpty(s));

			var body = String.Join("\n\n", bodyParts);

			var declaration = template.Replace(FORMAT_ITEM, body);

			return declaration;
		}

		#region State

		private String GetSynchronizationContextDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = GetName(typeDeclaration);
			var synchronizeMethod = GetSynchronizeMethod(typeDeclaration);
			var desynchronizMethod = GetDesynchronizeMethod(typeDeclaration);

			var declaration =
$@"private sealed class SynchronizationContext
{{
public SynchronizationContext({name} instance)
{{
{CONTEXT_INSTANCE_NAME} = instance;
}}

public Boolean IsSynchronized => _synchronizationState == 1;

private readonly {name} {CONTEXT_INSTANCE_NAME}; 
private System.Int32 _synchronizationState = 0;

public void Invoke(Action whenSynchronized = null, Action whenDesynchronized = null)
{{
if (whenSynchronized != null && System.Threading.Interlocked.CompareExchange(ref _synchronizationState, -1, 1) == 1)
{{
try
{{
whenSynchronized.Invoke();
}}
catch
{{
throw;
}}
finally
{{
_synchronizationState = 1;
}}
}}
else if (whenDesynchronized != null && System.Threading.Interlocked.CompareExchange(ref _synchronizationState, -1, 0) == 0)
{{
try
{{
whenDesynchronized.Invoke();
}}
catch
{{
throw;
}}
finally
{{
_synchronizationState = 0;
}}
}}
}}

{synchronizeMethod}
{desynchronizMethod}
}}

private SynchronizationContext __synchronizationState;
private SynchronizationContext GetSynchronizationContext()
{{
return __synchronizationState ??= new SynchronizationContext(this);
}}";

			return declaration;

		}

		#region Desynchronize
		private String GetDesynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var unsubscriptions = String.Join("\n", GetUnsubscriptionExpressions(typeDeclaration));

			var declaration =
$@"public void Desynchronize()
{{
if(System.Threading.Interlocked.CompareExchange(ref _synchronizationState, -1, 1) == 1)
{{
try
{{
var authority = {CONTEXT_INSTANCE_NAME}?.{authority};
if(authority != null)
{{
{unsubscriptions}
}}

_synchronizationState = 0;
}}
catch
{{
_synchronizationState = 1;
throw;
}}
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
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInState: true);
			var propertyName = GetGeneratedPropertyName(field);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);

			var expression =
$@"authority.Unsubscribe(typeId: {typeIdAccess}, 
propertyName: ""{propertyName}"", 
sourceInstanceId: {sourceInstanceIdAccess}, 
instanceId: {instanceIdAccess});";

			return expression;
		}

		#endregion

		#region Synchronize
		private String GetSynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authority = GetAuthorityPropertyName(typeDeclaration);
			var subscriptions = String.Join("\n", GetSubscriptionExpressions(typeDeclaration));
			var pulls = String.Join("\n", GetPullExpressions(typeDeclaration));

			var initializer =
$@"public void Synchronize()
{{
if(System.Threading.Interlocked.CompareExchange(ref _synchronizationState, -1, 0) == 0)
{{
try
{{
var authority = {CONTEXT_INSTANCE_NAME}?.{authority};
if(authority != null)
{{
{pulls}

{subscriptions}
}}

_synchronizationState = 1;
}}
catch
{{
_synchronizationState = 0;
throw;
}}
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

			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInState: true);
			var propertyName = GetGeneratedPropertyName(field);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);

			var subscription =
$@"{CONTEXT_INSTANCE_NAME}.{fieldName} = authority.Pull<{fieldType}>(typeId: {typeIdAccess},
propertyName: ""{propertyName}"",
sourceInstanceId: {sourceInstanceIdAccess},
instanceId: {instanceIdAccess});";

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

			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInState: true);
			var propertyName = GetGeneratedPropertyName(field);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInState: true);
			var setDelegate = GetSetDelegate(field, fromWithinContext: true);

			var subscription =
$@"authority.Subscribe<{fieldType}>(typeId: {typeIdAccess}, 
propertyName: ""{propertyName}"", 
sourceInstanceId: {sourceInstanceIdAccess}, 
instanceId: {instanceIdAccess}, 
callback: {setDelegate});";

			return subscription;
		}
		#endregion
		#endregion

		#region Ids
		private String GetIdDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var typeIdDeclaration = TryGetTypeIdProperty(typeDeclaration, out var _) ?
									String.Empty :
									GetTypeIdPropertyDeclaration(typeDeclaration);
			var sourceInstanceIdDeclaration = TryGetSourceInstanceIdProperty(typeDeclaration, out var _) ?
									String.Empty :
									GetSourceInstanceIdPropertyDeclaration(typeDeclaration);
			var instanceIdDeclaration = TryGetInstanceIdProperty(typeDeclaration, out var _) ?
									String.Empty :
									GetInstanceIdPropertyDeclaration(typeDeclaration);

			var declarations = String.Join("\n", new[] { typeIdDeclaration, sourceInstanceIdDeclaration, instanceIdDeclaration }.Where(s => !String.IsNullOrEmpty(s)));

			return declarations;
		}

		private Boolean TryGetIdProperty(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, out PropertyDeclarationSyntax idProperty)
		{
			var properties = _analyzer.GetPropertyDeclarations(typeDeclaration, include: new[] { idAttributeIdentifier });
			ThrowIfMultiple(properties, "properties", idAttributeIdentifier, typeDeclaration);

			idProperty = properties.SingleOrDefault();

			return idProperty != null;
		}
		private String GetIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, String fallbackName)
		{
			var name = TryGetIdProperty(typeDeclaration, idAttributeIdentifier, out var property) ?
				property.Identifier.Text :
				fallbackName;

			return name;
		}
		private String GetIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic)
		{
			var declaration = TryGetIdProperty(typeDeclaration, idAttributeIdentifier, out _) ?
				String.Empty :
				"private " + (defaultIsStatic ? "static " : String.Empty) + $"System.String {fallbackName} {{ get; }} = System.Guid.NewGuid().ToString();";

			return declaration;
		}
		private String GetIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic, Boolean accessingInState)
		{
			var propertyName = GetIdPropertyName(typeDeclaration, idAttributeIdentifier, fallbackName);

			TryGetIdProperty(typeDeclaration, idAttributeIdentifier, out var idProperty);
			var isStatic = idProperty?.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)) ?? defaultIsStatic;

			String access = null;

			if (isStatic)
			{
				var name = GetName(typeDeclaration);
				access = $"{name}.{propertyName}";
			}
			else if (accessingInState)
			{
				access = $"{CONTEXT_INSTANCE_NAME}.{propertyName}";
			}
			else
			{
				access = propertyName;
			}

			return access;
		}

		#region Type Id
		private Boolean TryGetTypeIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(typeDeclaration, TypeIdAttributeIdentifier, out idProperty);
		}
		private String GetTypeIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyDeclaration(typeDeclaration, TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_IS_STATIC);
		}
		private String GetTypeIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInState)
		{
			return GetIdPropertyAccess(typeDeclaration, TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_IS_STATIC, accessingInState);
		}
		private String GetTypeIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyName(typeDeclaration, TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME);
		}
		#endregion

		#region Source Instance Id
		private Boolean TryGetSourceInstanceIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(typeDeclaration, SourceInstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetSourceInstanceIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyDeclaration(typeDeclaration, SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC);
		}
		private String GetSourceInstanceIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInState)
		{
			return GetIdPropertyAccess(typeDeclaration, SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC, accessingInState);
		}
		private String GetSourceInstanceIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyName(typeDeclaration, SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME);
		}
		#endregion

		#region Instance Id
		private Boolean TryGetInstanceIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(typeDeclaration, InstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetInstanceIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyDeclaration(typeDeclaration, InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_IS_STATIC);
		}
		private String GetInstanceIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInState)
		{
			return GetIdPropertyAccess(typeDeclaration, InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_IS_STATIC, accessingInState);
		}
		private String GetInstanceIdPropertyName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyName(typeDeclaration, InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME);
		}
		#endregion
		#endregion

		#region Events
		private String GetEventMethodDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var methods = _analyzer.GetFieldDeclarations(typeDeclaration)
				.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, GenerateEventsAttributeIdentifier)) ?
@"partial void OnPropertyChanging(System.String propertyName);
partial void OnPropertyChanged(System.String propertyName);" :
String.Empty;

			return methods;
		}
		#endregion

		#region Properties
		private IEnumerable<String> GetGeneratedPropertyDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var properties = fields.Select(f => GetGeneratedPropertyDeclaration(f, typeDeclaration));

			return properties;
		}
		private String GetGeneratedPropertyDeclaration(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setter = GetGeneratedPropertySetterBody(field, typeDeclaration);

			var property =
$@"public {fieldType} {propertyName} 
{{
get
{{
return {fieldName};
}}
set
{{
{setter}
}}
}}";

			return property;
		}

		private String GetGeneratedPropertySetterBody(FieldDeclarationSyntax field, BaseTypeDeclarationSyntax typeDeclaration)
		{
			var propertyChangingCall = GetPropertyChangingCall(field, false);
			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);
			var propertyChangedCall = GetPropertyChangedCall(field, false);
			var propertyName = GetGeneratedPropertyName(field);
			var authorityName = GetAuthorityPropertyName(typeDeclaration);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInState: false);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInState: false);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInState: false);
			var setDelegate = GetSetDelegate(field, fromWithinContext: false);

			var body = _analyzer.HasAttribute(field.AttributeLists, field, SynchronizedAttributeIdentifier) ?
$@"GetSynchronizationContext().Invoke(whenSynchronized: () =>
{{{propertyChangingCall}
{fieldName} = value;{propertyChangedCall}
{authorityName}?.Push<{fieldType}>(typeId: {typeIdAccess}, 
propertyName: ""{propertyName}"", 
sourceInstanceId: {sourceInstanceIdAccess}, 
instanceId: {instanceIdAccess}, value);
}}, whenDesynchronized: {setDelegate});" :
$@"{propertyChangingCall}
{fieldName} = value;{propertyChangedCall}";

			return body;
		}

		private String GetSetDelegate(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var parameter = fromWithinContext ?
				"value" :
				String.Empty;
			var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_NAME}." :
				String.Empty;
			var fieldName = GetFieldName(field);
			var propertyChangedCall = GetPropertyChangedCall(field, fromWithinContext);

			var del =
$@"({parameter}) =>
{{{propertyChangingCall}
{instance}{fieldName} = value;{propertyChangedCall}
}}";

			return del;
		}

		private String GetPropertyChangingCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_NAME}." :
				String.Empty;
			var call = _analyzer.HasAttribute(field.AttributeLists, field, GenerateEventsAttributeIdentifier) ?
				$"\n{instance}OnPropertyChanging(propertyName: \"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"{CONTEXT_INSTANCE_NAME}." :
				String.Empty;
			var call = _analyzer.HasAttribute(field.AttributeLists, field, GenerateEventsAttributeIdentifier) ?
				$"\n{instance}OnPropertyChanged(propertyName: \"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			_ = _analyzer.TryGetAttributes(field.AttributeLists, field, SynchronizedAttributeIdentifier, out var attributes);

			var propertyNameArgument = attributes.Single()
				.ArgumentList?
				.Arguments
				.Single();

			var propertyName = String.Empty;

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
				}
				else if (children[0] is IdentifierNameSyntax identifier &&
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
				else if (_analyzer.HasAttribute(field.AttributeLists, field, SynchronizedAttributeIdentifier))
				{
					propertyName = getPrefixedName(SYNCHRONIZED_PROPERTY_PREFIX);
				}
				else if (_analyzer.HasAttribute(field.AttributeLists, field, GenerateEventsAttributeIdentifier))
				{
					propertyName = getPrefixedName(EVENTFUL_PROPERTY_PREFIX);
				}
				else
				{
					propertyName = getPrefixedName(DEFAULT_PROPERTY_PREFIX);
				}

				String getPrefixedName(String prefix)
				{
					return String.Concat(prefix, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
				}
			}

			return propertyName;
		}

		#endregion

		private IEnumerable<FieldDeclarationSyntax> GetFields(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return _analyzer.GetFieldDeclarations(typeDeclaration, new[] { SynchronizedAttributeIdentifier, GenerateEventsAttributeIdentifier });
		}
		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private String GetFieldType(FieldDeclarationSyntax field)
		{
			return _analyzer.GetTypeIdentifier(field.Declaration.Type).ToString();
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
