using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RhoMicro.CodeAnalysis;
using RhoMicro.ObjectSync.Attributes;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace RhoMicro.ObjectSync.Generator
{
    [Generator]
    internal class Generator : ISourceGenerator
    {
        private static readonly IEnumerable<GeneratedType> _attributeTypes = new[]
            {
                GeneratedAttributes.TypeId.GeneratedType,
                GeneratedAttributes.SourceInstanceId.GeneratedType,
                GeneratedAttributes.InstanceId.GeneratedType,
                GeneratedAttributes.Synchronized.GeneratedType,
                GeneratedAttributes.SynchronizationAuthority.GeneratedType,
                GeneratedAttributes.SynchronizationTarget.GeneratedType,
                GeneratedAttributes.TypeExportConfiguration.GeneratedType,
                GeneratedAttributes.Accessibility,
                GeneratedAttributes.Modifier,
                GeneratedAttributes.ExportConfigType
            }.ToImmutableList();
        private static readonly IEnumerable<GeneratedSource> _attributeSources = _attributeTypes.Select(t => t.Source).ToImmutableList();

        public void Execute(GeneratorExecutionContext context)
        {
            if(!(context.SyntaxContextReceiver is ObjectSyncSyntaxContextReceiver objectSyncReceiver))
            {
                return;
            }

            var sources = objectSyncReceiver.Types
                .Select(t => SourceFactory.GetSource(t, context.Compilation.GetSemanticModel(t.SyntaxTree), objectSyncReceiver.Config))
                .Concat(
                    new Func<TypeExportConfigurationAttribute, GeneratedType>[]
                    {
                        GeneratedSynchronizationClasses.GetISynchronizationAuthority,
                        GeneratedSynchronizationClasses.GetStaticSynchronizationAuthority,
                        GeneratedSynchronizationClasses.GetSynchronizationAuthorityBase,
                        GeneratedSynchronizationClasses.GetSyncInfo,
                        GeneratedSynchronizationClasses.GetInitializable
                    }
                    .Select(f => f.Invoke(objectSyncReceiver.Config))
                    .Select(t => t.Source))
                .Where(s => s != default);

#if DEBUG
            Console.WriteLine(String.Join("\n\n", sources));
            Console.ReadLine();
#endif

            context.AddSources(sources);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(c => c.AddSources(_attributeSources));

            context.RegisterForSyntaxNotifications(() => new ObjectSyncSyntaxContextReceiver());
        }

        private sealed class ObjectSyncSyntaxContextReceiver : ISyntaxContextReceiver
        {
            public HashSet<BaseTypeDeclarationSyntax> Types { get; } = new HashSet<BaseTypeDeclarationSyntax>();
            public TypeExportConfigurationAttribute Config { get; private set; } = new TypeExportConfigurationAttribute();
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if(context.Node is BaseTypeDeclarationSyntax ||
                    context.Node is FieldDeclarationSyntax &&
                    (context.SemanticModel.GetDeclaredSymbol(context.Node)?.HasAttributes(GeneratedAttributes.Synchronized.GeneratedType.Identifier) ?? false) ||
                    context.Node is PropertyDeclarationSyntax &&
                    (context.SemanticModel.GetDeclaredSymbol(context.Node)?
                                         .HasAttributes(GeneratedAttributes.InstanceId.GeneratedType.Identifier,
                                                        GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier,
                                                        GeneratedAttributes.TypeId.GeneratedType.Identifier,
                                                        GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier) ?? false))
                {
                    var node = context.Node;

                    do
                    {
                        if(node is BaseTypeDeclarationSyntax declaration)
                        {
                            if(!Types.Contains(declaration))
                            {
                                var match = context.SemanticModel.GetDeclaredSymbol(declaration)?
                                    .HasAttributes(GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier) ?? false;

                                if(match)
                                {
                                    _ = Types.Add(declaration);
                                }
                            }

                            return;
                        }

                        node = node.Parent;
                    } while(node != null);
                } else if(context.Node is AttributeSyntax attribute &&
                      GeneratedAttributes.TypeExportConfiguration.Factory.TryBuild(attribute, context.SemanticModel, out var config))
                {
                    Config = config;
                }
            }
        }
    }
}
