using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Synchronization;
using RhoMicro.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace ObjectSync.Generator
{
	[Generator]
	internal class Generator : ISourceGenerator
	{
		private static IEnumerable<GeneratedType> AttributeTypes = new[]
			{
				GeneratedAttributes.TypeId.GeneratedType,
				GeneratedAttributes.SourceInstanceId.GeneratedType,
				GeneratedAttributes.InstanceId.GeneratedType,
				GeneratedAttributes.Synchronized.GeneratedType,
				GeneratedAttributes.SynchronizationAuthority.GeneratedType,
				GeneratedAttributes.SynchronizationTarget.GeneratedType,
				GeneratedAttributes.Attributes
			}.ToImmutableList();
		private static IEnumerable<GeneratedSource> AttributeSources = AttributeTypes.Select(t => t.Source).ToImmutableList();

		private static IEnumerable<GeneratedType> SynchronizationClassesTypes = new[]
			{
				GeneratedSynchronizationClasses.ISynchronizationAuthority,
				GeneratedSynchronizationClasses.StaticSynchronizationAuthority,
				GeneratedSynchronizationClasses.SynchronizationAuthorityBase,
				GeneratedSynchronizationClasses.SyncInfo,
				GeneratedSynchronizationClasses.Initializable
			}.ToImmutableList();
		private static IEnumerable<GeneratedSource> SynchronizationClassesSources = SynchronizationClassesTypes.Select(t => t.Source).ToImmutableList();

		public void Execute(GeneratorExecutionContext context)
		{
			if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
			{
				return;
			}

			var sources = receiver.Types.Select(t => SourceFactory.GetSource(t, context.Compilation.GetSemanticModel(t.SyntaxTree)));

#if DEBUG
			Console.WriteLine(String.Join("\r\n\r\n", sources));
			Console.ReadLine();
#endif

			context.AddSources(sources);
		}

		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForPostInitialization(c =>
			{
				c.AddSources(AttributeSources);
				c.AddSources(SynchronizationClassesSources);
			});

			context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
		}

		private sealed class SyntaxReceiver : ISyntaxContextReceiver
		{
			public HashSet<BaseTypeDeclarationSyntax> Types { get; } = new HashSet<BaseTypeDeclarationSyntax>();

			public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
			{
				if (context.Node is BaseTypeDeclarationSyntax ||
					(context.Node is FieldDeclarationSyntax &&
					(context.SemanticModel.GetDeclaredSymbol(context.Node)?.HasAttributes(GeneratedAttributes.Synchronized.GeneratedType.Identifier) ?? false)) ||
					(context.Node is PropertyDeclarationSyntax &&
					(context.SemanticModel.GetDeclaredSymbol(context.Node)?
										 .HasAttributes(GeneratedAttributes.InstanceId.GeneratedType.Identifier,
														GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier,
														GeneratedAttributes.TypeId.GeneratedType.Identifier,
														GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier) ?? false)))
				{
					var node = context.Node;

					do
					{
						if (node is BaseTypeDeclarationSyntax declaration)
						{
							if (!Types.Contains(declaration))
							{
								var match = context.SemanticModel.GetDeclaredSymbol(declaration)?
									.HasAttributes(GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier) ?? false;

								if (match)
								{
									Types.Add(declaration);
								}
							}

							return;
						}

						node = node.Parent;
					} while (node != null);
				}
			}
		}
	}
}
