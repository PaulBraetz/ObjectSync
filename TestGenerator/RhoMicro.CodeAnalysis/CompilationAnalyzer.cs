using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RhoMicro.CodeAnalysis
{
    internal sealed class CompilationAnalyzer
    {
        private readonly Compilation _compilation;
        public IEnumerable<BaseTypeDeclarationSyntax> TypeDeclarations { get; }

        public CompilationAnalyzer(Compilation compilation)
        {
            _compilation = compilation;

            TypeDeclarations = _compilation.SyntaxTrees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()).ToArray();
        }

        public IEnumerable<BaseTypeDeclarationSyntax> GetTypeDeclarations(IEnumerable<TypeIdentifier> include = null, IEnumerable<TypeIdentifier> exclude = null)
        {
            var typeDeclarations = TypeDeclarations;

            if (include == null)
            {
                include = Array.Empty<TypeIdentifier>();
            }
            if (exclude == null)
            {
                exclude = Array.Empty<TypeIdentifier>();
            }
            if(!exclude.Any() && !include.Any())
            {
                return typeDeclarations;
            }

            return typeDeclarations.Where(d => !exclude.Any(a => HasAttribute(d.AttributeLists, d, a)) && include.Any(a => HasAttribute(d.AttributeLists, d, a)));
        }

        public IEnumerable<FieldDeclarationSyntax> GetFieldDeclarations(BaseTypeDeclarationSyntax typeDeclaration, IEnumerable<TypeIdentifier> include = null, IEnumerable<TypeIdentifier> exclude = null)
        {
            var fields = typeDeclaration.ChildNodes().OfType<FieldDeclarationSyntax>();

            if (include == null)
            {
                include = Array.Empty<TypeIdentifier>();
            }
            if (exclude == null)
            {
                exclude = Array.Empty<TypeIdentifier>();
            }
            if (!exclude.Any() && !include.Any())
            {
                return fields;
            }

            return fields.Where(d => !exclude.Any(a => HasAttribute(d.AttributeLists, d, a)) && include.Any(a => HasAttribute(d.AttributeLists, d, a)));
        }

        public IEnumerable<PropertyDeclarationSyntax> GetPropertyDeclarations(BaseTypeDeclarationSyntax typeDeclaration, IEnumerable<TypeIdentifier> include = null, IEnumerable<TypeIdentifier> exclude = null)
        {
            var properties = typeDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>();

            if (include == null)
            {
                include = Array.Empty<TypeIdentifier>();
            }
            if (exclude == null)
            {
                exclude = Array.Empty<TypeIdentifier>();
            }
            if (!exclude.Any() && !include.Any())
            {
                return properties;
            }

            return properties.Where(d => !exclude.Any(a => HasAttribute(d.AttributeLists, d, a)) && include.Any(a => HasAttribute(d.AttributeLists, d, a)));
        }

        public TypeIdentifier GetTypeArgumentOrDefault(SyntaxList<AttributeListSyntax> attributeLists, SyntaxNode node, TypeIdentifier attributeIdentifier)
        {
            return GetTypeArguments(attributeLists, node, attributeIdentifier).SingleOrDefault();
        }
        public IEnumerable<TypeIdentifier> GetTypeArguments(SyntaxList<AttributeListSyntax> attributeLists, SyntaxNode node, TypeIdentifier attributeIdentifier)
        {
            if (TryGetAttributes(attributeLists, node, attributeIdentifier, out var attributes))
            {
                var modelTypeSyntaxes = attributes.SelectMany(a => a.DescendantNodes()).OfType<TypeOfExpressionSyntax>().Select(e => e.Type);
                var arguments = modelTypeSyntaxes.Select(GetTypeIdentifier);

                return arguments;
            }

            return Array.Empty<TypeIdentifier>();
        }
        public Boolean TryGetAttributes(SyntaxList<AttributeListSyntax> attributeLists, SyntaxNode node, TypeIdentifier attributeIdentifier, out IEnumerable<AttributeSyntax> attributes)
        {
            var availableUsings = GetAvailableUsings(node);
            var usingNamespace = availableUsings.Contains(attributeIdentifier.Namespace);

            attributes = attributeLists.SelectMany(al => al.Attributes).Where(a => equals(a));

            return attributes.Any();

            Boolean equals(AttributeSyntax attributeSyntax)
            {
                return attributeSyntax.Name.ToString() == attributeIdentifier.ToString() ||
                    usingNamespace && attributeSyntax.Name.ToString() == attributeIdentifier.Name.ToString();

            }
        }

        public Boolean HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, SyntaxNode node, TypeIdentifier attributeIdentifier)
        {
            var availableUsings = GetAvailableUsings(node);
            var usingNamespace = availableUsings.Contains(attributeIdentifier.Namespace);

            return attributeLists.SelectMany(al => al.Attributes).Any(equals);

            Boolean equals(AttributeSyntax attributeSyntax)
            {
                return attributeSyntax.Name.ToString() == attributeIdentifier.ToString() ||
                    usingNamespace && attributeSyntax.Name.ToString() == attributeIdentifier.Name.ToString();

            }
        }

        public IEnumerable<Namespace> GetAvailableUsings(SyntaxNode node)
        {
            var result = new List<Namespace>();

            while (node.Parent != null)
            {
                var namespaces = node.Parent.ChildNodes().OfType<UsingDirectiveSyntax>();

                foreach (var @namespace in namespaces)
                {
                    var item = Namespace.Create()
                        .WithRange(@namespace.Name.ToString().Split('.'));

                    result.Add(item);
                }

                node = node.Parent;
            }

            return result;
        }

        public TypeIdentifier GetTypeIdentifier(TypeSyntax type)
        {
            var semanticModel = _compilation.GetSemanticModel(type.SyntaxTree);
            var symbol = semanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? semanticModel.GetTypeInfo(type).Type;

            var identifier = TypeIdentifier.Create(symbol);

            return identifier;
        }
        public TypeIdentifier GetTypeIdentifier(PropertyDeclarationSyntax property)
        {
            var semanticModel = _compilation.GetSemanticModel(property.SyntaxTree);
            var symbol = semanticModel.GetDeclaredSymbol(property).Type;

            var identifier = TypeIdentifier.Create(symbol);

            return identifier;
        }
        public TypeIdentifier GetTypeIdentifier(BaseTypeDeclarationSyntax declaration)
        {
            var semanticModel = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var symbol = semanticModel.GetDeclaredSymbol(declaration);

            var identifier = TypeIdentifier.Create(symbol);

            return identifier;
        }
    }
}
