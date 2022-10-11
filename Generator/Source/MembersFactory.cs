using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObjectSync.Generator
{
	internal sealed partial class SourceFactory
	{
		private sealed class MembersFactory
		{
			private readonly SourceFactory _parent;

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
								SyntaxFactory.ParseTypeName($"{GeneratedSynchronizationClasses.GetInitializable(_parent.Declared.ExportConfig).Identifier}<{_parent.Context.TypeName}>"))
							.AddVariables(
								SyntaxFactory.VariableDeclarator(ContextFieldName)
								.WithInitializer(
									SyntaxFactory.EqualsValueClause(
										SyntaxFactory.ParseExpression($"new {GeneratedSynchronizationClasses.GetInitializable(_parent.Declared.ExportConfig).Identifier}<{_parent.Context.TypeName}>()")))))
							.AddModifiers(
								SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
								SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
							.WithSemicolonToken(
								SyntaxFactory.Token(SyntaxKind.SemicolonToken));
					}

					return _contextField;
				}
			}

			private PropertyDeclarationSyntax _context;
			public PropertyDeclarationSyntax Context
			{
				get
				{
					return _context ??
						(_context = SyntaxFactory.PropertyDeclaration(
							_parent.Context.TypeSyntax,
							_parent.Declared.SynchronizationTargetAttribute.ContextPropertyName)
						.AddModifiers(
							_parent.Declared.SynchronizationTargetAttribute.ContextPropertyAccessibility
							.AsSyntax()
							.Concat(
								_parent.Declared.SynchronizationTargetAttribute.ContextPropertyModifier.AsSyntax())
							.Select(SyntaxFactory.Token)
							.ToArray())
						.AddAccessorListAccessors(
							SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(
							SyntaxFactory.ParseStatement(
$@"if(!this.{ContextField.Declaration.Variables.Single().Identifier}.IsAssigned)
{{
	this.{ContextField.Declaration.Variables.Single().Identifier}.Assign(new {_parent.Context.TypeName}(this));
}}
return this.{ContextField.Declaration.Variables.Single().Identifier};"))));
				}
			}

			private PropertyDeclarationSyntax _typeId;
			public PropertyDeclarationSyntax TypeId
			{
				get
				{
					return _typeId ?? (_typeId = GetIdPropertyDeclaration(_parent.TypeIdAttributeIdentifier,
									   _parent.Members.TypeIdDefaultPropertyName,
									   _parent.Members.TypeIdDefaultPropertySummary,
									   _parent.Members.TypeIdDefaultPropertyIsStatic,
									   _parent.Members.TypeIdDefaultPropertyHasSetter));
				}
			}

			private PropertyDeclarationSyntax _sourceInstanceId;
			public PropertyDeclarationSyntax SourceInstanceId
			{
				get
				{
					return _sourceInstanceId ?? (_sourceInstanceId = GetIdPropertyDeclaration(_parent.SourceInstanceIdAttributeIdentifier,
																			   _parent.Members.SourceInstanceIdDefaultPropertyName,
																			   _parent.Members.SourceInstanceIdDefaultPropertySummary,
																			   _parent.Members.SourceInstanceIdDefaultPropertyIsStatic,
																			   _parent.Members.SourceInstanceIdDefaultPropertyHasSetter));
				}
			}

			private PropertyDeclarationSyntax _instanceId;
			public PropertyDeclarationSyntax InstanceId
			{
				get
				{
					return _instanceId ?? (_instanceId = GetIdPropertyDeclaration(_parent.InstanceIdAttributeIdentifier,
																			   _parent.Members.InstanceIdDefaultPropertyName,
																			   _parent.Members.InstanceIdDefaultPropertySummary,
																			   _parent.Members.InstanceIdDefaultPropertyIsStatic,
																			   _parent.Members.InstanceIdDefaultPropertyHasSetter));
				}
			}

			private PropertyDeclarationSyntax[] _generatedProperties;
			public PropertyDeclarationSyntax[] GeneratedProperties
			{
				get
				{
					if (_generatedProperties == null)
					{

						_generatedProperties = _parent.Declared.SynchronizedFields.Select(f => GetGeneratedPropertyDeclaration(f)).ToArray();
					}

					return _generatedProperties;
				}
			}
			#endregion

			public MembersFactory(SourceFactory parent)
			{
				_parent = parent ?? throw new ArgumentNullException(nameof(parent));
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
								SyntaxKind.GetAccessorDeclaration)
							.WithSemicolonToken(
								SyntaxFactory.Token(
									SyntaxKind.SemicolonToken)));

					if (defaultHasSetter)
					{
						property = property
							.AddAccessorListAccessors(
								SyntaxFactory.AccessorDeclaration(
									SyntaxKind.SetAccessorDeclaration)
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
						.WithLeadingTrivia(fallbackSummary.AsLeadingTrivia());
				}
				else
				{
					property = null;
				};

				return property;
			}

			private Boolean TryGetIdProperty(TypeIdentifier idAttributeIdentifier, out PropertyDeclarationSyntax idProperty)
			{
				var properties = _parent.Declared.Properties.Where(p => p.AttributeLists.HasAttributes(_parent.Declared.SemanticModel, idAttributeIdentifier)).ToArray();
				_parent.ThrowIfMultiple(properties, "properties", idAttributeIdentifier);

				idProperty = properties.SingleOrDefault();

				return idProperty != null;
			}

			private PropertyDeclarationSyntax GetGeneratedPropertyDeclaration(FieldDeclarationSyntax field)
			{
				var comment = String.Join("\r\r\n", field.DescendantTrivia()
					.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)));

				comment = String.IsNullOrEmpty(comment) ?
					comment :
					$"{comment}\r\r\n";

				var fieldType = _parent.GetFieldType(field);
				var fieldName = _parent.GetFieldName(field);

				var propertyName = _parent.GetGeneratedPropertyName(field);

				var attribute = GetAttribute(field);
				var setStatement = _parent.GetSetStatement(field, fromWithinContext: false);

				var isFast = attribute.Fast;

				var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration);

				if (isFast)
				{
					setter = setter
					.AddBodyStatements(
						setStatement,
						SyntaxFactory.ParseStatement(
$@"if(this.{ContextPropertyName}.{_parent.Context.IsSynchronizedPropertyName})
{{
#nullable disable
	this.{ContextPropertyName}.{_parent.Context.PushMethodName}<{fieldType}>(""{fieldName}"", value);
#nullable restore
}}"));
				}
				else
				{
					setter = setter
					.AddBodyStatements(
						SyntaxFactory.ParseStatement(
$@"this.{ContextPropertyName}.{_parent.Context.InvokeMethodName}((isSynchronized) =>
{{
	{setStatement}
	if(isSynchronized)
	{{		
#nullable disable
		this.{ContextPropertyName}.{_parent.Context.PushMethodName}<{fieldType}>(""{fieldName}"", value);
#nullable restore
	}}
}});"));
				}

				var property = SyntaxFactory.PropertyDeclaration(
						field.Declaration.Type,
						propertyName)
					.AddModifiers(attribute.PropertyAccessibility.AsSyntax().Select(SyntaxFactory.Token).ToArray())
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
						.AddBodyStatements(SyntaxFactory.ParseStatement($"return {fieldName};")),
						setter)
					.WithLeadingTrivia(SyntaxFactory.Comment(comment));

				return property;
			}

			private SynchronizedAttribute GetAttribute(FieldDeclarationSyntax field)
			{
				var attributeSyntax = field.AttributeLists.OfAttributeClasses(_parent.Declared.SemanticModel, _parent.SynchronizedAttributeIdentifier).Single();
				var attribute = _parent.SynchronizedAttributeFactory.TryBuild(attributeSyntax, _parent.Declared.SemanticModel, out var a) ?
					a :
					throw new Exception($"Unable to examine attribute for {field}");

				return attribute;
			}
		}

	}
}
