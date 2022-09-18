using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ObjectSync.Generator
{
	internal sealed class PartialTypeSource : IDisposable
	{
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
		private const String OBSERVABLE_PROPERTY_PREFIX = "Observable";
		private const String DEFAULT_PROPERTY_PREFIX = "Generated";
		private const String FORMAT_ITEM = "{" + nameof(FORMAT_ITEM) + "}";
		#endregion

		#region Aliae
		private static TypeIdentifier TypeIdAttributeIdentifier => GeneratedAttributes.TypeId.GeneratedType.Identifier;
		private static TypeIdentifier InstanceIdAttributeIdentifier => GeneratedAttributes.InstanceId.GeneratedType.Identifier;
		private static TypeIdentifier SourceInstanceIdAttributeIdentifier => GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier;
		private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier => GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier;
		private static TypeIdentifier SynchronizedAttributeIdentifier => GeneratedAttributes.Synchronized.GeneratedType.Identifier;
		#endregion

		public PartialTypeSource(BaseTypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel)
		{
			_semanticModel = semanticModel;
			_typeDeclaration = typeDeclaration;
		}

		public static GeneratedSource GetSource(BaseTypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel)
		{
			using (var partialSource = new PartialTypeSource(typeDeclaration, semanticModel))
			{
				return partialSource.GetSource();
			}
		}

		private BaseTypeDeclarationSyntax _typeDeclaration;
		private SemanticModel _semanticModel;

		private Optional<GeneratedSource> _generatedSource;
		public GeneratedSource GetSource()
		{
			if (!_generatedSource.HasValue)
			{
				var className = GetName();
				String source;
				try
				{
					source = GetGeneratedTypeDeclaration();
				}
				catch (Exception ex)
				{
					source = GetError(ex);
				}

				_generatedSource = new GeneratedSource(source, className);
			}

			return _generatedSource.Value;
		}

		private String GetError(Exception ex)
		{
			var error =
$@"/*
An error occured while generating this source file for {GetName()}:
{ex}
*/";

			return error;
		}

		private String GetGeneratedTypeDeclaration()
		{
			var template = GetClassTemplate();

			var synchronizationContextDeclaration = GetSynchronizationContextDeclaration();
			var idDeclarations = GetIdDeclarations();
			var eventMethods = GetEventMethodDeclarations();

			var propertyDeclarations = GetGeneratedPropertyDeclarations();

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
		private String GetSynchronizationContextDeclaration()
		{
			if (!HasSynchronizedFields())
			{
				return String.Empty;
			}

			var name = GetName();
			var synchronizeMethod = GetSynchronizeMethod();
			var desynchronizeMethod = GetDesynchronizeMethod();
			var resynchroniezMethod = GetResynchronizeMethod();
			var desynchronizeInvokeSynchronizeMethod = GetDesynchronizeInvokeSynchronizeMethod();

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

		private String GetDesynchronizeMethod()
		{
			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions();

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
		private String GetSynchronizeMethod()
		{
			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);

			var subscriptions = GetRevertableSubscriptions();
			var pulls = GetPulls();
			var pullAssignments = GetPullAssignments();

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
		private String GetResynchronizeMethod()
		{
			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions();

			var subscriptions = GetRevertableSubscriptions();
			var pulls = GetPulls();
			var pullAssignments = GetPullAssignments();

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
		private String GetDesynchronizeInvokeSynchronizeMethod()
		{
			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: true);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: true);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: true);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: true);
			var unsubscriptions = GetRevertableUnsubscriptions();

			var subscriptions = GetRevertableSubscriptions();
			var pulls = GetPulls();
			var pullAssignments = GetPullAssignments();

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

		private String _revertableUnsubscriptions;
		private String GetRevertableUnsubscriptions()
		{
			if (_revertableUnsubscriptions == null)
			{
				var fields = GetSynchronizedFields();
				var requiredRevertions = new List<FieldDeclarationSyntax>();
				var expressions = String.Join("\n", fields.Select(f => GetRevertableUnsubscription(f, requiredRevertions)));

				_revertableUnsubscriptions = expressions;
			}

			return _revertableUnsubscriptions;
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

		private String _pulls;
		private String GetPulls()
		{
			if (_pulls == null)
			{
				var fields = GetSynchronizedFields();
				var expressions = String.Join("\n", fields.Select(GetPull));

				_pulls = expressions;
			}

			return _pulls;
		}
		private String GetPull(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);

			var expression = $@"var valueOf{propertyName} = authority.Pull<{fieldType}>(typeId, ""{propertyName}"", sourceInstanceId, instanceId);";

			return expression;
		}

		private String _pullAssignments;
		private String GetPullAssignments()
		{
			if (_pullAssignments == null)
			{
				var fields = GetSynchronizedFields();
				var expressions = String.Join("\n", fields.Select(GetPullAssignment));

				_pullAssignments = expressions;
			}

			return _pullAssignments;
		}
		private String GetPullAssignment(FieldDeclarationSyntax field)
		{
			var propertyName = GetGeneratedPropertyName(field);
			var fieldName = GetFieldName(field);

			var expression = $"_instance.{fieldName} = valueOf{propertyName};";

			return expression;
		}

		private String _revertableSubscriptions;
		private String GetRevertableSubscriptions()
		{
			if (_revertableSubscriptions == null)
			{
				var fields = GetSynchronizedFields();

				var requiredRevertions = new List<FieldDeclarationSyntax>();
				var expressions = String.Join("\n", fields.Select(f => GetRevertableSubscription(f, requiredRevertions)));

				_revertableSubscriptions = expressions;
			}

			return _revertableSubscriptions;
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
		private String GetIdDeclarations()
		{
			if (!HasSynchronizedFields())
			{
				return String.Empty;
			}

			var typeIdDeclaration = TryGetTypeIdProperty(out var _) ?
									String.Empty :
									GetTypeIdPropertyDeclaration();
			var sourceInstanceIdDeclaration = TryGetSourceInstanceIdProperty(out var _) ?
									String.Empty :
									GetSourceInstanceIdPropertyDeclaration();
			var instanceIdDeclaration = TryGetInstanceIdProperty(out var _) ?
									String.Empty :
									GetInstanceIdPropertyDeclaration();

			var declarations = String.Join("\n", new[] { typeIdDeclaration, sourceInstanceIdDeclaration, instanceIdDeclaration }.Where(s => !String.IsNullOrEmpty(s)));

			return declarations;
		}

		private Boolean TryGetIdProperty(TypeIdentifier idAttributeIdentifier, out PropertyDeclarationSyntax idProperty)
		{
			var properties = GetProperties().Where(p => p.AttributeLists.HasAttributes(_semanticModel, idAttributeIdentifier));
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
		private String GetIdPropertyDeclaration(TypeIdentifier idAttributeIdentifier, String fallbackName, String fallbackSummary, Boolean defaultIsStatic, Boolean defaultHasSetter)
		{
			var declaration = TryGetIdProperty(idAttributeIdentifier, out _) ?
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
		private String GetIdPropertyAccess(TypeIdentifier idAttributeIdentifier, String fallbackName, Boolean defaultIsStatic, Boolean accessingInContext)
		{
			var propertyName = GetIdPropertyName(idAttributeIdentifier, fallbackName);

			_ = TryGetIdProperty(idAttributeIdentifier, out var idProperty);
			var isStatic = idProperty?.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)) ?? defaultIsStatic;

			String access = null;

			if (isStatic)
			{
				var name = GetName();
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
		private Boolean TryGetTypeIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(TypeIdAttributeIdentifier, out idProperty);
		}
		private String GetTypeIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_SUMMARY, TYPE_ID_DEFAULT_IS_STATIC, TYPE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetTypeIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(TypeIdAttributeIdentifier, TYPE_ID_DEFAULT_NAME, TYPE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion

		#region Source Instance Id
		private Boolean TryGetSourceInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(SourceInstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetSourceInstanceIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_SUMMARY, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC, SOURCE_INSTANCE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetSourceInstanceIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(SourceInstanceIdAttributeIdentifier, SOURCE_INSTANCE_ID_DEFAULT_NAME, SOURCE_INSTANCE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion

		#region Instance Id
		private Boolean TryGetInstanceIdProperty(out PropertyDeclarationSyntax idProperty)
		{
			return TryGetIdProperty(InstanceIdAttributeIdentifier, out idProperty);
		}
		private String GetInstanceIdPropertyDeclaration()
		{
			return GetIdPropertyDeclaration(InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_SUMMARY, INSTANCE_ID_DEFAULT_IS_STATIC, INSTANCE_ID_DEFAULT_HAS_SETTER);
		}
		private String GetInstanceIdPropertyAccess(Boolean accessingInContext)
		{
			return GetIdPropertyAccess(InstanceIdAttributeIdentifier, INSTANCE_ID_DEFAULT_NAME, INSTANCE_ID_DEFAULT_IS_STATIC, accessingInContext);
		}
		#endregion
		#endregion

		#region Events
		private String GetEventMethodDeclarations()
		{
			var methods = HasObservableFields() ?
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
		private IEnumerable<String> GetGeneratedPropertyDeclarations()
		{
			var fields = GetSynchronizedFields();
			var properties = fields.Select(f => GetGeneratedPropertyDeclaration(f));

			return properties;
		}
		private String GetGeneratedPropertyDeclaration(FieldDeclarationSyntax field)
		{
			var comment = String.Join("\n", field.DescendantTrivia()
				.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)));

			comment = String.IsNullOrEmpty(comment) ?
				comment :
				$"{comment}\n";

			var fieldType = GetFieldType(field);
			var fieldName = GetFieldName(field);

			var propertyName = GetGeneratedPropertyName(field);
			var setter = GetGeneratedPropertySetterBody(field);

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

		private String GetGeneratedPropertySetterBody(FieldDeclarationSyntax field)
		{
			var fieldType = GetFieldType(field);
			var propertyName = GetGeneratedPropertyName(field);
			var authorityAccess = GetAuthorityPropertyAccess(accessingInContext: false);
			var typeIdAccess = GetTypeIdPropertyAccess(accessingInContext: false);
			var sourceInstanceIdAccess = GetSourceInstanceIdPropertyAccess(accessingInContext: false);
			var instanceIdAccess = GetInstanceIdPropertyAccess(accessingInContext: false);
			var setBlock = GetSetBlock(field, fromWithinContext: false);

			var attributeInstance = field.AttributeLists
				.SelectMany(al => al.Attributes)
				.Select(a => (success: GeneratedAttributes.Synchronized.Factory.TryBuild(a, _semanticModel, out var instance), instance))
				.FirstOrDefault(t => t.success).instance;
			var isFastSynchronized = attributeInstance?.Observable ?? false;

			var isSynchronized = isFastSynchronized || field.AttributeLists.HasAttributes(_semanticModel, SynchronizedAttributeIdentifier);

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
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: GeneratedAttributes.Synchronized.Factory.TryBuild(a, _semanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}OnPropertyChanging(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}
		private String GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var instance = fromWithinContext ?
				$"_instance." :
				String.Empty;
			var call = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: GeneratedAttributes.Synchronized.Factory.TryBuild(a, _semanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				$"\n{instance}OnPropertyChanged(\"{GetGeneratedPropertyName(field)}\");" :
				String.Empty;

			return call;
		}

		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			var attributeInstance = field.AttributeLists.SelectMany(al => al.Attributes)
				.Select(a => (success: GeneratedAttributes.Synchronized.Factory.TryBuild(a, _semanticModel, out var instance), instance))
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
		#endregion

		private Optional<Boolean> _hasObservableFields;
		private Boolean HasObservableFields()
		{
			if (!_hasObservableFields.HasValue)
			{
				var fields = GetSynchronizedFields();
				var hasObservable = fields
					.Where(f => f.AttributeLists.HasAttributes(_semanticModel, SynchronizedAttributeIdentifier))
					.Any(f =>
					{
						var match = f.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: GeneratedAttributes.Synchronized.Factory.TryBuild(a, _semanticModel, out var instance), instance))
						.FirstOrDefault(t => t.success).instance?.Observable ?? false;

						return match;
					});

				_hasObservableFields = hasObservable;
			}

			return _hasObservableFields.Value;
		}
		private Optional<Boolean> _hasSynchronizedFields;
		private Boolean HasSynchronizedFields()
		{
			if (!_hasSynchronizedFields.HasValue)
			{
				var fields = GetSynchronizedFields();
				var hasSynchronized = fields.Any();

				_hasSynchronizedFields = hasSynchronized;
			}

			return _hasSynchronizedFields.Value;
		}

		private IEnumerable<FieldDeclarationSyntax> _synchronizedFields;
		private IEnumerable<FieldDeclarationSyntax> GetSynchronizedFields()
		{
			return _synchronizedFields ?? (_synchronizedFields = _typeDeclaration.ChildNodes().OfType<FieldDeclarationSyntax>().Where(f => f.AttributeLists.HasAttributes(_semanticModel, SynchronizedAttributeIdentifier)).ToList().AsReadOnly());
		}

		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private String GetFieldType(FieldDeclarationSyntax field)
		{
			var type = field.Declaration.Type;
			var symbol = _semanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? _semanticModel.GetTypeInfo(type).Type;

			var identifier = TypeIdentifier.Create(symbol).ToString();

			return identifier;
		}

		private IEnumerable<PropertyDeclarationSyntax> _properties;
		private IEnumerable<PropertyDeclarationSyntax> GetProperties()
		{
			return _properties ?? (_properties = _typeDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly());
		}

		private String GetAuthorityPropertyAccess(Boolean accessingInContext)
		{
			var identifier = GetIdentifier();

			var authorityProperties = GetProperties().Where(p => p.AttributeLists.HasAttributes(_semanticModel, SynchronizationAuthorityAttributeIdentifier));
			ThrowIfMultiple(authorityProperties, "properties", SynchronizationAuthorityAttributeIdentifier);

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

		private String GetClassTemplate()
		{
			var template = GetClassTemplate(_typeDeclaration);

			return template;
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

		private Optional<TypeIdentifier> _identifier;
		private TypeIdentifier GetIdentifier()
		{
			if (!_identifier.HasValue)
			{
				var identifier = TypeIdentifier.Create(_semanticModel.GetDeclaredSymbol(_typeDeclaration));
				_identifier = identifier;
			}

			return _identifier.Value;
		}

		private String _name;
		private String GetName()
		{
			return _name ?? (_name = GetName(_typeDeclaration));
		}
		private String GetName(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var name = typeDeclaration.Identifier.Text;

			return name;
		}

		private String GetNamespace(SyntaxNode node)
		{
			while (node.Parent != null && !(node is BaseNamespaceDeclarationSyntax))
			{
				node = node.Parent;
			}

			var @namespace = node == null ?
				String.Empty :
				(node as BaseNamespaceDeclarationSyntax).Name.ToString();

			return @namespace;
		}

		private String GetModifiers(BaseTypeDeclarationSyntax typeDeclaration)
		{
			var modifiers = String.Join(" ", typeDeclaration.Modifiers);

			return modifiers;
		}

		private void ThrowIfMultiple<T>(IEnumerable<T> items, String declarationType, TypeIdentifier attribute)
		{
			if (items.Count() > 1)
			{
				throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {GetIdentifier()}.");
			}
		}

		private Boolean disposedValue;
		public void Dispose()
		{
			if (!disposedValue)
			{
				_hasObservableFields = default;
				_hasSynchronizedFields = default;
				_identifier = default;

				_name = null;
				_properties = null;
				_synchronizedFields = null;
				_revertableUnsubscriptions = null;
				_pulls = null;
				_pullAssignments = null;
				_revertableSubscriptions = null;

				_typeDeclaration = null;
				_semanticModel = null;

				disposedValue = true;
			}
		}
	}
}
