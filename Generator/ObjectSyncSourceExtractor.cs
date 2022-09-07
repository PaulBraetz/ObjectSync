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
		private static readonly TypeIdentifierName FastSynchronizedAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.FastSynchronizedAttribute>();
		private static readonly TypeIdentifierName SynchronizationAuthorityAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.SynchronizationAuthorityAttribute>();
		private static readonly TypeIdentifierName ObservableAttributeName = TypeIdentifierName.CreateAttribute<ObjectSync.Attributes.ObservableAttribute>();

		private static readonly TypeIdentifier SynchronizedAttributeIdentifier = TypeIdentifier.Create(SynchronizedAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier FastSynchronizedAttributeIdentifier = TypeIdentifier.Create(FastSynchronizedAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier SynchronizationAuthorityAttributeIdentifier = TypeIdentifier.Create(SynchronizationAuthorityAttributeName, AttributesNamespace);
		private static readonly TypeIdentifier ObservableAttributeIdentifier = TypeIdentifier.Create(ObservableAttributeName, AttributesNamespace);
		#endregion

		#region Constants
		private const String TYPE_ID_DEFAULT_NAME = "TypeId";
		private const String TYPE_ID_DEFAULT_SUMMARY = "The Id identifying this instance's type.";
		private const Boolean TYPE_ID_DEFAULT_IS_STATIC = true;
		private const Boolean TYPE_ID_DEFAULT_HAS_SETTER = false;

		private const String SOURCE_INSTANCE_ID_DEFAULT_NAME = "SourceInstanceId";
		private const String SOURCE_INSTANCE_ID_DEFAULT_SUMMARY = "The Id identifying this instance's property data source.";
		private const Boolean SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC = false;
		private const Boolean SOURCE_INSTANCE_ID_DEFAULT_HAS_SETTER = true;

		private const String INSTANCE_ID_DEFAULT_NAME = "InstanceId";
		private const String INSTANCE_ID_DEFAULT_SUMMARY = "The Id identifying this instance.";
		private const Boolean INSTANCE_ID_DEFAULT_IS_STATIC = false;
		private const Boolean INSTANCE_ID_DEFAULT_HAS_SETTER = false;

		private const String SYNCHRONIZED_PROPERTY_PREFIX = "Synchronized";
		private const String AUTO_NOTIFY_PROPERTY_PREFIX = "Observable";
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
				.Where(td => HasObservableFields(td) || HasSynchronizedFields(td));

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

		#region Context
		private String GetSynchronizationContextDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			if (!HasSynchronizedFields(typeDeclaration))
			{
				return String.Empty;
			}

			var name = GetName(typeDeclaration);
			var synchronizeMethod = GetSynchronizeMethod(typeDeclaration);
			var desynchronizeMethod = GetDesynchronizeMethod(typeDeclaration);
			var resynchroniezMethod = GetResynchronizeMethod(typeDeclaration);
			var desynchronizeInvokeSynchronizeMethod = GetDesynchronizeInvokeSynchronizeMethod(typeDeclaration);

			var declaration =
$@"/// <summary>
/// The context encapsulating the synchronization state of <see cref=""{name}""/> instances.
/// </summary>
private sealed class {name}SynchronizationContext
{{
	public {name}SynchronizationContext({name} instance)
	{{
		_instance = instance ?? throw new System.ArgumentNullException(""instance"");
	}}

	/// <summary>
	/// Invoked after <see cref=""IsSynchronized""/> has changed.
	/// </summary>
	public event System.EventHandler<System.Boolean> SynchronizationChanged;

	/// <summary>
	/// The instance whose synchronized properties are to be managed.
	/// </summary>
	private readonly {name} _instance;

	/// <summary>
	/// Sync object for synchronizing access to synchronization logic.
	/// </summary>
	private readonly System.Object _syncRoot = new System.Object();

	/// <summary>
	/// Backing field for <see cref""IsSynchronized""/>.
	/// </summary>
	private System.Boolean _isSynchronized;
	/// <summary>
	/// Indicates wether the instance is synchronized.
	/// </summary>
	public System.Boolean IsSynchronized => _isSynchronized;

	/// <summary>
	/// Invokes the methods provided in a threadsafe manner relative to the synchronization methods.
	/// This means that the synchronization state of the instance is guaranteed not to change during the invocation.
	/// The method will be passed the synchronization state at the time of invocation.
	/// <para>
	/// Invoking any method of this instance in <paramref name=""method""/> will likely cause a deadlock to occur.
	/// </para>
	/// </summary>
	/// <param name = ""method"">The method to invoke.</param>
	public void Invoke(System.Action<System.Boolean> method)
	{{
		if (method != null)
		{{
			lock (_syncRoot)
			{{
				method.Invoke(_isSynchronized);
			}}
		}}
	}}

	{synchronizeMethod}

	{desynchronizeMethod}

	{resynchroniezMethod}

	{desynchronizeInvokeSynchronizeMethod}
}}

/// <summary>
/// Backing field for <see cref=""SynchronizationContext""/>.
/// </summary>
private {name}SynchronizationContext __synchronizationContext;
/// <summary>
/// The context responsible for managing this instance's property synchronization.
/// </summary>
private {name}SynchronizationContext SynchronizationContext
{{
	get => __synchronizationContext ??= new {name}SynchronizationContext(this);
}}";
			return declaration;
		}

		private String GetDesynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authorityAccess = GetAuthorityPropertyAccess(typeDeclaration, accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions(typeDeclaration);

			var declaration =
$@"/// <summary>
/// Desynchronizes the instance if it is synchronized.
/// </summary>
public void Desynchronize()
{{
	if (_isSynchronized)
	{{
		lock (_syncRoot)
		{{
			if (_isSynchronized)
			{{
				var authority = {authorityAccess};
				if (authority != null)
				{{
					var typeId = {typeIdAccess};
					var sourceInstanceId = {sourceInstanceIdAccess};
					var instanceId = {instanceIdAccess};

					{unsubscriptions}
				}}

				{GetIsSynchronizedSet(false, false)}
			}}
		}}
	}}
}}";

			return declaration;
		}
		private String GetSynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authorityAccess = GetAuthorityPropertyAccess(typeDeclaration, accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions(typeDeclaration);

			var subscriptions = GetRevertableSubscriptions(typeDeclaration);
			var pulls = GetPulls(typeDeclaration);
			var pullAssignments = GetPullAssignments(typeDeclaration);

			var declaration =
$@"/// <summary>
/// Synchronizes the instance if it is desynchronized.
/// </summary>
public void Synchronize()
{{
	if(!_isSynchronized)
	{{
		lock (_syncRoot)
		{{
			if(!_isSynchronized)
			{{
				var authority = {authorityAccess};
				if (authority != null)
				{{
					var typeId = {typeIdAccess};
					var sourceInstanceId = {sourceInstanceIdAccess};
					var instanceId = {instanceIdAccess};

					{subscriptions}

					{pulls}

					{pullAssignments}
				}}

				{GetIsSynchronizedSet(true, false)}
			}}
		}}
	}}
}}";

			return declaration;
		}
		private String GetResynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authorityAccess = GetAuthorityPropertyAccess(typeDeclaration, accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions(typeDeclaration);

			var subscriptions = GetRevertableSubscriptions(typeDeclaration);
			var pulls = GetPulls(typeDeclaration);
			var pullAssignments = GetPullAssignments(typeDeclaration);

			var declaration =
$@"/// <summary>
/// Synchronizes the instance.
/// If it is synchronized already, it is first desynchronized.
/// </summary>
public void Resynchronize()
{{
	lock (_syncRoot)
	{{
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
	}}
}}";

			return declaration;
		}
		private String GetDesynchronizeInvokeSynchronizeMethod(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var authorityAccess = GetAuthorityPropertyAccess(typeDeclaration, accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions(typeDeclaration);

			var subscriptions = GetRevertableSubscriptions(typeDeclaration);
			var pulls = GetPulls(typeDeclaration);
			var pullAssignments = GetPullAssignments(typeDeclaration);

			var declaration =
$@"/// <summary>
/// If <paramref name=""method""/> is not null, desynchronizes the instance, invokes <paramref name=""method""/>, and synchronizes the instance again, all in a threadsafe manner.
/// This means that the synchronization state of the instance is guaranteed not to change during the invocation.
/// <para>
/// Invoking any method of this instance in <paramref name=""method""/> will likely cause a deadlock to occur.
/// </para>
/// </summary>
/// <param name=""method"">The method to invoke after desynchronizing.</param>
public void DesynchronizeInvokeSynchronize(System.Action method)
{{
	if(method != null)
	{{
		lock (_syncRoot)
		{{
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

				method.Invoke();

				{subscriptions}

				{pulls}

				{pullAssignments}

				{GetIsSynchronizedSet(true, false)}
			}}
			else
			{{
				method.Invoke();

				{GetIsSynchronizedSet(true, true)}
			}}
		}}
	}}
}}";

			return declaration;
		}

		private String GetRevertableUnsubscriptions(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var requiredRevertions = new List<FieldDeclarationSyntax>();
			var expressions = String.Join("\n", fields.Select(f => GetRevertableUnsubscription(f, requiredRevertions)));

			return expressions;
		}
		private String GetRevertableUnsubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
		{
			requiredRevertions.Add(field);

			var unsubscription = GetUnsubscription(field);
			var revertion = String.Join("\n", requiredRevertions.Select(f => GetSubscription(f)));

			var expression =
$@"try
{{
	{unsubscription}
}}
catch
{{
	//revert
	{revertion}
	throw;
}}";
			return expression;
		}
		private String GetUnsubscription(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);

			var expression = $@"authority.Unsubscribe(typeId, ""{propertyName}"", sourceInstanceId, instanceId);";

			return expression;
		}

		private String GetPulls(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var expressions = String.Join("\n", fields.Select(GetPull));

			return expressions;
		}
		private String GetPull(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);

			var expression = $@"var valueOf{propertyName} = authority.Pull<{fieldType}>(typeId, ""{propertyName}"", sourceInstanceId, instanceId);";

			return expression;
		}

		private String GetPullAssignments(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var expressions = String.Join("\n", fields.Select(GetPullAssignment));

			return expressions;
		}
		private String GetPullAssignment(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);
			var fieldName = GetFieldName(field);

			var expression = $"_instance.{fieldName} = valueOf{propertyName};";

			return expression;
		}

		private String GetRevertableSubscriptions(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);

			var requiredRevertions = new List<FieldDeclarationSyntax>();
			var expressions = String.Join("\n", fields.Select(f => GetRevertableSubscription(f, requiredRevertions)));

			return expressions;
		}
		private String GetRevertableSubscription(FieldDeclarationSyntax field, List<FieldDeclarationSyntax> requiredRevertions)
		{
			requiredRevertions.Add(field);

			var subscription = GetSubscription(field);
			var revertion = String.Join("\n", requiredRevertions.Select(f => GetUnsubscription(f)));

			var expression =
$@"try
{{
	{subscription}
}}
catch
{{
	//revert
	{revertion}
	throw;
}}";
			return expression;
		}
		private String GetSubscription(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setBlock = GetSetBlock(field, fromWithinContext: true);

			var expression = $@"authority.Subscribe<{fieldType}>(typeId, ""{propertyName}"", sourceInstanceId, instanceId, (value) => {{{setBlock}}});";

			return expression;
		}

		private String GetIsSynchronizedSet(Boolean value, Boolean checkChanged)
		{
			var valueString = value ? "true" : "false";
			var inversion = value ? "!" : String.Empty;

			var expression = checkChanged ?
$@"if({inversion}_isSynchronized)
{{
	_isSynchronized = {valueString};
	SynchronizationChanged?.Invoke(this, {valueString});
}}" :
$@"_isSynchronized = {valueString};
SynchronizationChanged?.Invoke(this, {valueString});";

			return expression;
		}
		#endregion

		#region Ids
		private String GetIdDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			if (!HasSynchronizedFields(typeDeclaration))
			{
				return String.Empty;
			}

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
		private String GetIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, String fallbackName, String fallbackSummary, Boolean defaultIsStatic, Boolean defaultHasSetter)
		{
			var declaration = TryGetIdProperty(typeDeclaration, idAttributeIdentifier, out _) ?
				String.Empty :
$@"/// <summary>
/// {fallbackSummary}
/// </summary>
private " + (defaultIsStatic ? "static " : String.Empty) + $@"System.String {fallbackName} 
{{ 
	get; " + (defaultHasSetter ? @"
set;  " : String.Empty) +
"} = System.Guid.NewGuid().ToString();";

			return declaration;
		}
		private String GetIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic, Boolean accessingInContext)
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
			else if (accessingInContext)
			{
				access = $"_instance.{propertyName}";
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
			return GetIdPropertyDeclaration(typeDeclaration, TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_SUMMARY, TYPE_ID_DEFAULT_IS_STATIC, TYPE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetTypeIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInContext)
		{
			return GetIdPropertyAccess(typeDeclaration, TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion

		#region Source Instance Id
		private Boolean TryGetSourceInstanceIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(typeDeclaration, SourceInstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetSourceInstanceIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyDeclaration(typeDeclaration, SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_SUMMARY, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC, SOURCE_INSTANCE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetSourceInstanceIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInContext)
		{
			return GetIdPropertyAccess(typeDeclaration, SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion

		#region Instance Id
		private Boolean TryGetInstanceIdProperty(BaseTypeDeclarationSyntax typeDeclaration, out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(typeDeclaration, InstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetInstanceIdPropertyDeclaration(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return GetIdPropertyDeclaration(typeDeclaration, InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_SUMMARY, INSTANCE_ID_DEFAULT_IS_STATIC, INSTANCE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetInstanceIdPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInContext)
		{
			return GetIdPropertyAccess(typeDeclaration, InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion
		#endregion

		#region Events
		private String GetEventMethodDeclarations(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var methods = _analyzer.GetFieldDeclarations(typeDeclaration)
				.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, ObservableAttributeIdentifier)) ?
@"/// <summary>
/// Invoked when a property value is changing.
/// </summary>
partial void OnPropertyChanging(System.String propertyName);
/// <summary>
/// Invoked when a property value has changed.
/// </summary>
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
			var comment = String.Join("\n", field.DescendantTrivia()
				.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)));

			comment = String.IsNullOrEmpty(comment) ?
				comment :
				$"{comment}\n";

			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setter = GetGeneratedPropertySetterBody(field, typeDeclaration);

			var property =
$@"{comment}public {fieldType} {propertyName} 
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
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);
			var authorityAccess = GetAuthorityPropertyAccess(typeDeclaration, accessingInContext: false);
			var typeIdAccess = GetTypeIdPropertyAccess(typeDeclaration, accessingInContext: false);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(typeDeclaration, accessingInContext: false);
			var instanceIdAccess = GetInstanceIdPropertyAccess(typeDeclaration, accessingInContext: false);
			var setBlock = GetSetBlock(field, fromWithinContext: false);

			var isFastSynchronized = _analyzer.HasAttribute(field.AttributeLists, field, FastSynchronizedAttributeIdentifier);

			var isSynchronized = isFastSynchronized || _analyzer.HasAttribute(field.AttributeLists, field, SynchronizedAttributeIdentifier);

			var body = isSynchronized ?
					isFastSynchronized ?
$@"{setBlock}
if(SynchronizationContext.IsSynchronized)
{{		
	{authorityAccess}?.Push<{fieldType}>({typeIdAccess}, ""{propertyName}"", {sourceInstanceIdAccess}, {instanceIdAccess}, value);
}}" :
$@"SynchronizationContext.Invoke((isSynchronized) =>
{{
	{setBlock}
	if(isSynchronized)
	{{		
		{authorityAccess}?.Push<{fieldType}>({typeIdAccess}, ""{propertyName}"", {sourceInstanceIdAccess}, {instanceIdAccess}, value);
	}}
}});" :
setBlock;

			return body;
		}

		private String GetSetBlock(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
			var instance = fromWithinContext ?
				"_instance." :
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
				$"_instance." :
				String.Empty;
			var call = _analyzer.HasAttribute(field.AttributeLists, field, ObservableAttributeIdentifier) ?
				$"\n{instance}OnPropertyChanging(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"_instance." :
				String.Empty;
			var call = _analyzer.HasAttribute(field.AttributeLists, field, ObservableAttributeIdentifier) ?
				$"\n{instance}OnPropertyChanged(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			IEnumerable<AttributeSyntax> fastAttributes = null;
			IEnumerable<AttributeSyntax> slowAttributes = null;
			IEnumerable<AttributeSyntax> observableAttributes = null;

			var isSynchronized = _analyzer.TryGetAttributes(field.AttributeLists, field, FastSynchronizedAttributeIdentifier, out fastAttributes) |
								 _analyzer.TryGetAttributes(field.AttributeLists, field, SynchronizedAttributeIdentifier, out slowAttributes);

			var isObservable = _analyzer.TryGetAttributes(field.AttributeLists, field, ObservableAttributeIdentifier, out observableAttributes);

			var propertyNameArgument =
				fastAttributes?.SingleOrDefault()?
					.ArgumentList?
					.Arguments
					.SingleOrDefault() ??
				slowAttributes?.SingleOrDefault()?
					.ArgumentList?
					.Arguments
					.SingleOrDefault() ??
				observableAttributes?.SingleOrDefault()?
					.ArgumentList?
					.Arguments
					.SingleOrDefault();

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
				else if (isSynchronized)
				{
					propertyName = getPrefixedName(SYNCHRONIZED_PROPERTY_PREFIX);
				}
				else if (_analyzer.HasAttribute(field.AttributeLists, field, ObservableAttributeIdentifier))
				{
					propertyName = getPrefixedName(AUTO_NOTIFY_PROPERTY_PREFIX);
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

		private Boolean HasObservableFields(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var hasObservable = fields.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, ObservableAttributeIdentifier));

			return hasObservable;
		}
		private Boolean HasSynchronizedFields(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var fields = GetFields(typeDeclaration);
			var hasSynchronized = fields.Any(f => _analyzer.HasAttribute(f.AttributeLists, f, FastSynchronizedAttributeIdentifier) || _analyzer.HasAttribute(f.AttributeLists, f, SynchronizedAttributeIdentifier));

			return hasSynchronized;
		}

		private IEnumerable<FieldDeclarationSyntax> GetFields(BaseTypeDeclarationSyntax typeDeclaration)
		{
			return _analyzer.GetFieldDeclarations(typeDeclaration, new[] { SynchronizedAttributeIdentifier, FastSynchronizedAttributeIdentifier, ObservableAttributeIdentifier });
		}
		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private String GetFieldType(FieldDeclarationSyntax field)
		{
			return _analyzer.GetTypeIdentifier(field.Declaration.Type).ToString();
		}

		private String GetAuthorityPropertyAccess(BaseTypeDeclarationSyntax typeDeclaration, Boolean accessingInContext)
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

			var access = accessingInContext ?
				$"_instance.{authorityName}" :
				authorityName;

			return access;
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
