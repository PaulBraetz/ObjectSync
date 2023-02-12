using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using RhoMicro.ObjectSync.Attributes;

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RhoMicro.ObjectSync
{
    internal sealed partial class SourceFactory
    {
        #region Aliae
        private static TypeIdentifier TypeIdAttributeIdentifier => GeneratedAttributes.TypeId.GeneratedType.Identifier;
        private static TypeIdentifier InstanceIdAttributeIdentifier => GeneratedAttributes.InstanceId.GeneratedType.Identifier;
        private static TypeIdentifier SourceInstanceIdAttributeIdentifier => GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier;
        private static TypeIdentifier SynchronizationAuthorityAttributeIdentifier => GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier;
        private static TypeIdentifier SynchronizationTargetAttributeIdentifier => GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier;
        private static TypeIdentifier SynchronizedAttributeIdentifier => GeneratedAttributes.Synchronized.GeneratedType.Identifier;

        private static IAttributeFactory<TypeIdAttribute> TypeIdAttributeFactory => GeneratedAttributes.TypeId.Factory;
        private static IAttributeFactory<InstanceIdAttribute> InstanceIdAttributeFactory => GeneratedAttributes.InstanceId.Factory;
        private static IAttributeFactory<SourceInstanceIdAttribute> SourceInstanceIdAttributeFactory => GeneratedAttributes.SourceInstanceId.Factory;
        private static IAttributeFactory<SynchronizationAuthorityAttribute> SynchronizationAuthorityAttributeFactory => GeneratedAttributes.SynchronizationAuthority.Factory;
        private static IAttributeFactory<SynchronizationTargetAttribute> SynchronizationTargetAttributeFactory => GeneratedAttributes.SynchronizationTarget.Factory;
        private static IAttributeFactory<SynchronizedAttribute> SynchronizedAttributeFactory => GeneratedAttributes.Synchronized.Factory;
        #endregion

        private DeclaredInfo Declared
        {
            get;
        }
        private ContextFactory Context
        {
            get;
        }
        private MembersFactory Members
        {
            get;
        }

        private Optional<GeneratedSource> _generatedSource;

        public SourceFactory(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel, TypeExportConfigurationAttribute exportConfig)
        {
            Declared = new DeclaredInfo(synchronizedType, semanticModel, exportConfig, this);
            Members = new MembersFactory(this);
            Context = new ContextFactory(this);
        }

        public static GeneratedSource GetSource(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel, TypeExportConfigurationAttribute exportConfig)
        {
            var source = new SourceFactory(synchronizedType, semanticModel, exportConfig).GetSource();

            return source;
        }

        public GeneratedSource GetSource()
        {
            if(!_generatedSource.HasValue)
            {
                var name = Declared.TypeIdentifier;

                try
                {
                    var source = GetNamespaceDeclaration();

                    _generatedSource = new GeneratedSource(source, name.ToNonGenericString());
                } catch(Exception ex)
                {
                    var source =
$@"/*
An error occured while generating this source file for {Declared.TypeIdentifier}:
{ex}
*/";
                    _generatedSource = new GeneratedSource(source, name.ToNonGenericString());
                }
            }

            return _generatedSource.Value;
        }

        #region Type
        private NamespaceDeclarationSyntax GetNamespaceDeclaration()
        {
            var namespaceName = TryGetNamespace(Declared.Type, out var declaredNamespace) ?
                declaredNamespace.Name :
                throw new Exception($"{Declared.TypeIdentifier} was not found to be declared in a namespace.");

            var generatedTypeDeclaration = GetGeneratedTypeDeclaration();

            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(namespaceName)
                    .AddMembers(generatedTypeDeclaration);

            return namespaceDeclaration;
        }
        private MemberDeclarationSyntax[] GetGeneratedTypeMembers()
        {
            var members = new MemberDeclarationSyntax[]
                {
                    Context.GetDeclaration(),
                    Members.PropertyChangedEventMethod,
                    Members.PropertyChangingEventMethod,
                    Members.ContextField,
                    Members.Context,
                    Members.TypeId,
                    Members.SourceInstanceId,
                    Members.InstanceId
                }.Concat(Members.GeneratedProperties)
                .Where(m => m != null)
                .OrderBy(m => m.GetType().Name)
                .ToArray();

            return members;
        }
        private BaseTypeDeclarationSyntax GetGeneratedTypeDeclaration()
        {
            var synchronizedTypeDeclarationName = Declared.TypeIdentifier.Name.ToString();
            var generatedTypeMembers = GetGeneratedTypeMembers();

            var generatedTypeDeclaration = SyntaxFactory.TypeDeclaration(SyntaxKind.ClassDeclaration, synchronizedTypeDeclarationName)
                .WithModifiers(Declared.Type.Modifiers)
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(generatedTypeMembers))
                .WithLeadingTrivia(
                    SyntaxFactory.Trivia(
                        SyntaxFactory.NullableDirectiveTrivia(
                            SyntaxFactory.Token(SyntaxKind.RestoreKeyword),
                            true)))
                .WithTrailingTrivia(
                    SyntaxFactory.Trivia(
                        SyntaxFactory.NullableDirectiveTrivia(
                            SyntaxFactory.Token(SyntaxKind.DisableKeyword),
                            true)));

            return generatedTypeDeclaration;
        }
        #endregion

        #region Misc
        private StatementSyntax[] GetSetStatements(FieldDeclarationSyntax field, Boolean fromWithinContext)
        {
            var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
            var fieldName = GetFieldName(field);
            var propertyChangedCall = GetPropertyChangedCall(field, fromWithinContext);

            var statements = new StatementSyntax[]
            {
                propertyChangingCall,
                SyntaxFactory.ParseStatement($@"{(fromWithinContext ? $"{Context.InstancePropertyAccess.NormalizeWhitespace().ToFullString()}." : String.Empty)}{fieldName} = value;"),
                propertyChangedCall
            }
            .Where(s => s != null)
            .ToArray();

            return statements;
        }
        private StatementSyntax GetPropertyChangingCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
        {
            var statement = field.AttributeLists.SelectMany(al => al.Attributes)
                        .Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var attributeInstance), attributeInstance))
                        .FirstOrDefault(t => t.success)
                        .attributeInstance?
                        .Observable ?? false ?
                SyntaxFactory.ParseStatement($"{(fromWithinContext ? $"{Context.InstancePropertyAccess}." : String.Empty)}{MembersFactory.PropertyChangingEventMethodName}(\"{GetGeneratedPropertyName(field)}\");") :
                null;

            return statement;
        }
        private StatementSyntax GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
        {
            var statement = field.AttributeLists.SelectMany(al => al.Attributes)
                        .Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var attributeInstance), attributeInstance))
                        .FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
                 SyntaxFactory.ParseStatement($"{(fromWithinContext ? $"{Context.InstancePropertyAccess}." : String.Empty)}{MembersFactory.PropertyChangedEventMethodName}(\"{GetGeneratedPropertyName(field)}\");") :
                null;

            return statement;
        }

        private static String GetFieldName(FieldDeclarationSyntax field) => field.Declaration.Variables.Single().Identifier.Text;
        private TypeIdentifier GetFieldType(FieldDeclarationSyntax field)
        {
            var type = GetType(field.Declaration.Type);

            return type;
        }
        private TypeIdentifier GetType(TypeSyntax type)
        {
            var symbol = Declared.SemanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? Declared.SemanticModel.GetTypeInfo(type).Type;

            var identifier = TypeIdentifier.Create(symbol);

            return identifier;
        }
        private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
        {
            var attributeInstance = field.AttributeLists.SelectMany(al => al.Attributes)
                .Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var instance), instance))
                .FirstOrDefault(t => t.success).instance;

            var propertyName = attributeInstance?.PropertyName;
            var isObservable = attributeInstance?.Observable ?? false;

            if(String.IsNullOrEmpty(propertyName))
            {
                var fieldName = GetFieldName(field);

                if(fieldName[0] == '_' || fieldName[0] == Char.ToLowerInvariant(fieldName[0]))
                {
                    var sanitizedFieldName = Regex.Replace(fieldName, @"^_*", String.Empty);
                    propertyName = String.Concat(Char.ToUpperInvariant(sanitizedFieldName[0]), sanitizedFieldName.Substring(1, sanitizedFieldName.Length - 1));
                } else
                {
                    propertyName = isObservable ? getPrefixedName(MembersFactory.ObservablePropertyPrefix) : getPrefixedName(MembersFactory.SynchronizedPropertyPrefix);
                }

                String getPrefixedName(String prefix) => String.Concat(prefix, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
            }

            return propertyName;
        }
        private static Boolean TryGetNamespace(SyntaxNode node, out BaseNamespaceDeclarationSyntax namespaceDeclaration)
        {
            while(node.Parent != null && !(node is BaseNamespaceDeclarationSyntax))
            {
                node = node.Parent;
            }

            namespaceDeclaration = node == null ?
                null :
                (node as BaseNamespaceDeclarationSyntax);

            return namespaceDeclaration != null;
        }

        private void ThrowIfMultiple<T>(T[] items, String declarationType, TypeIdentifier attribute)
        {
            if(items.Length > 1)
            {
                throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {Declared.TypeIdentifier}.");
            }
        }
        #endregion
    }
}
