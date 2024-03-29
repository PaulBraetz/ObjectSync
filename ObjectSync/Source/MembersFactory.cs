﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RhoMicro.CodeAnalysis;
using RhoMicro.ObjectSync.Attributes;

using System;
using System.Linq;

namespace RhoMicro.ObjectSync
{
    internal sealed partial class SourceFactory
    {
        private sealed class MembersFactory
        {
            private readonly SourceFactory _parent;

            #region Properties
            public static String PropertyNameLocalIdentifier => "propertyName";

            public static String ContextFieldName => "_synchronizationContext";
            public static String ContextPropertyName => "SynchronizationContext";

            public static String ObservablePropertyPrefix => "Observable";
            public static String SynchronizedPropertyPrefix => "Synchronized";

            public static String PropertyChangingEventMethodName => "OnPropertyChanging";
            public static String PropertyChangedEventMethodName => "OnPropertyChanged";

            public static String TypeIdDefaultPropertyName => "TypeId";
            public static String TypeIdDefaultPropertySummary =>
@"/// <summary>
/// The Id identifying this instance's type.
/// </summary/>";
            public static Boolean TypeIdDefaultPropertyIsStatic => true;
            public static Boolean TypeIdDefaultPropertyHasSetter => false;

            public static String SourceInstanceIdDefaultPropertyName => "SourceInstanceId";
            public static String SourceInstanceIdDefaultPropertySummary =>
    @"/// <summary>
/// The Id identifying this instance's property data source.
/// </summary/>";
            public static Boolean SourceInstanceIdDefaultPropertyIsStatic => false;
            public static Boolean SourceInstanceIdDefaultPropertyHasSetter => true;

            public static String InstanceIdDefaultPropertyName => "InstanceId";
            public static String InstanceIdDefaultPropertySummary =>
    @"/// <summary>
/// The Id identifying this instance.
/// </summary/>";
            public static Boolean InstanceIdDefaultPropertyIsStatic => false;
            public static Boolean InstanceIdDefaultPropertyHasSetter => false;

            private FieldDeclarationSyntax _contextField;
            public FieldDeclarationSyntax ContextField
            {
                get
                {
                    if(_contextField == null)
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
                    return _typeId ?? (_typeId = GetIdPropertyDeclaration(TypeIdAttributeIdentifier,
                                       TypeIdDefaultPropertyName,
                                       TypeIdDefaultPropertySummary,
                                       TypeIdDefaultPropertyIsStatic,
                                       TypeIdDefaultPropertyHasSetter));
                }
            }

            private PropertyDeclarationSyntax _sourceInstanceId;
            public PropertyDeclarationSyntax SourceInstanceId
            {
                get
                {
                    return _sourceInstanceId ?? (_sourceInstanceId = GetIdPropertyDeclaration(SourceInstanceIdAttributeIdentifier,
                                                                               SourceInstanceIdDefaultPropertyName,
                                                                               SourceInstanceIdDefaultPropertySummary,
                                                                               SourceInstanceIdDefaultPropertyIsStatic,
                                                                               SourceInstanceIdDefaultPropertyHasSetter));
                }
            }

            private PropertyDeclarationSyntax _instanceId;
            public PropertyDeclarationSyntax InstanceId
            {
                get
                {
                    return _instanceId ?? (_instanceId = GetIdPropertyDeclaration(InstanceIdAttributeIdentifier,
                                                                               InstanceIdDefaultPropertyName,
                                                                               InstanceIdDefaultPropertySummary,
                                                                               InstanceIdDefaultPropertyIsStatic,
                                                                               InstanceIdDefaultPropertyHasSetter));
                }
            }

            private PropertyDeclarationSyntax[] _generatedProperties;
            public PropertyDeclarationSyntax[] GeneratedProperties
            {
                get
                {
                    if(_generatedProperties == null)
                    {

                        _generatedProperties = _parent.Declared.SynchronizedFields.Select(f => GetGeneratedPropertyDeclaration(f)).ToArray();
                    }

                    return _generatedProperties;
                }
            }

            private Optional<MethodDeclarationSyntax> _propertyChangingEventMethod;
            public MethodDeclarationSyntax PropertyChangingEventMethod
            {
                get
                {
                    if(!_propertyChangingEventMethod.HasValue)
                    {
                        var generateMethod = _parent.Declared.SynchronizedFields
                            .Select(field =>
                                field.AttributeLists
                                    .SelectMany(al => al.Attributes)
                                    .Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, _parent.Declared.SemanticModel, out var attributeInstance), attributeInstance))
                                    .FirstOrDefault(t => t.success)
                                    .attributeInstance?
                                    .Observable ?? false)
                            .Any(o => o);

                        if(generateMethod)
                        {
                            var method = SyntaxFactory.MethodDeclaration(
                                        SyntaxFactory.ParseTypeName("void"),
                                        PropertyChangingEventMethodName)
                                    .AddModifiers(
                                        SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                                    .AddParameterListParameters(
                                        SyntaxFactory.Parameter(
                                                SyntaxFactory.Identifier(PropertyNameLocalIdentifier))
                                            .WithType(TypeIdentifier.Create<String>().AsSyntax()))
                                    .WithSemicolonToken(
                                        SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                            _propertyChangingEventMethod = method;
                        } else
                        {
                            _propertyChangingEventMethod = new Optional<MethodDeclarationSyntax>();
                        }
                    }

                    return _propertyChangingEventMethod.Value;
                }
            }

            private Optional<MethodDeclarationSyntax> _propertyChangedEventMethod;
            public MethodDeclarationSyntax PropertyChangedEventMethod
            {
                get
                {
                    if(!_propertyChangedEventMethod.HasValue)
                    {
                        var generateMethod = _parent.Declared.SynchronizedFields
                            .Select(field =>
                                field.AttributeLists
                                    .SelectMany(al => al.Attributes)
                                    .Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, _parent.Declared.SemanticModel, out var attributeInstance), attributeInstance))
                                    .FirstOrDefault(t => t.success)
                                    .attributeInstance?
                                    .Observable ?? false)
                            .Any(o => o);

                        if(generateMethod)
                        {
                            var method = SyntaxFactory.MethodDeclaration(
                                        SyntaxFactory.ParseTypeName("void"),
                                        PropertyChangedEventMethodName)
                                    .AddModifiers(
                                        SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                                    .AddParameterListParameters(
                                        SyntaxFactory.Parameter(
                                                SyntaxFactory.Identifier(PropertyNameLocalIdentifier))
                                            .WithType(TypeIdentifier.Create<String>().AsSyntax()))
                                    .WithSemicolonToken(
                                        SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                            _propertyChangedEventMethod = method;
                        } else
                        {
                            _propertyChangedEventMethod = new Optional<MethodDeclarationSyntax>();
                        }
                    }

                    return _propertyChangedEventMethod.Value;
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

                if(!TryGetIdProperty(idAttributeIdentifier, out _) && _parent.Declared.SynchronizationTargetAttribute.BaseContextTypeName == null)
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

                    if(defaultHasSetter)
                    {
                        property = property
                            .AddAccessorListAccessors(
                                SyntaxFactory.AccessorDeclaration(
                                    SyntaxKind.SetAccessorDeclaration)
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxKind.SemicolonToken)));
                    }

                    if(defaultIsStatic)
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
                } else
                {
                    property = null;
                }

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
                var fieldName = GetFieldName(field);

                var propertyName = _parent.GetGeneratedPropertyName(field);

                var attribute = GetAttribute(field);
                var setStatements = _parent.GetSetStatements(field, fromWithinContext: false);

                var isFast = attribute.Fast;

                var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration);

                setter = isFast
                    ? setter
                    .AddBodyStatements(
                        setStatements
                            .Append(
                                SyntaxFactory.ParseStatement(
$@"if(this.{ContextPropertyName}.{ContextFactory.IsSynchronizedPropertyName})
{{
#nullable disable
	this.{ContextPropertyName}.{ContextFactory.PushMethodName}<{fieldType}>(""{fieldName}"", value);
#nullable restore
}}"))
                            .ToArray())
                    : setter
                    .AddBodyStatements(
                        SyntaxFactory.ParseStatement(
$@"this.{ContextPropertyName}.{ContextFactory.InvokeMethodName}((isSynchronized) =>
{{
	{String.Join("\r\n", setStatements.Select(s => s.NormalizeWhitespace().ToFullString()))}
	if(isSynchronized)
	{{		
#nullable disable
		this.{ContextPropertyName}.{ContextFactory.PushMethodName}<{fieldType}> (""{fieldName}"", value);
#nullable restore
	}}
}});"));

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
                var attributeSyntax = field.AttributeLists.OfAttributeClasses(_parent.Declared.SemanticModel, SynchronizedAttributeIdentifier).Single();
                var attribute = SynchronizedAttributeFactory.TryBuild(attributeSyntax, _parent.Declared.SemanticModel, out var a) ?
                    a :
                    throw new Exception($"Unable to examine attribute for {field}");

                return attribute;
            }
        }
    }
}
