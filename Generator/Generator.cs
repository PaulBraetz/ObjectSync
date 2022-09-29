using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ObjectSync.Generator
{
	[Generator]
	internal class Generator : ISourceGenerator
	{
		private static IEnumerable<GeneratedType> AttributeTypes = new[]
			{
				TypeId.GeneratedType,
				SourceInstanceId.GeneratedType,
				InstanceId.GeneratedType,
				Synchronized.GeneratedType,
				SynchronizationAuthority.GeneratedType,
				SynchronizationContext.GeneratedType
			}.ToImmutableList();
		private static IEnumerable<TypeIdentifier> AttributeIdentifiers = AttributeTypes.Select(t => t.Identifier).ToImmutableList();
		private static IEnumerable<GeneratedSource> AttributeSources = AttributeTypes.Select(t => t.Source).ToImmutableList();

		private static IEnumerable<GeneratedType> SynchronizationClassesTypes = new[]
			{
				GeneratedSynchronizationClasses.ISynchronizationAuthority,
				GeneratedSynchronizationClasses.ISynchronizationContext,
				GeneratedSynchronizationClasses.StaticSynchronizationAuthority,
				GeneratedSynchronizationClasses.SynchronizationAuthorityBase,
				GeneratedSynchronizationClasses.SyncInfo
			}.ToImmutableList();
		private static IEnumerable<TypeIdentifier> SynchronizationClassesIdentifiers = SynchronizationClassesTypes.Select(t => t.Identifier).ToImmutableList();
		private static IEnumerable<GeneratedSource> SynchronizationClassesSources = SynchronizationClassesTypes.Select(t => t.Source).ToImmutableList();

		public void Execute(GeneratorExecutionContext context)
		{
			if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
			{
				return;
			}

			var synchronizedAttributeSymbol = Synchronized.GeneratedType.ExtractSymbol(context.Compilation);

			var sources = receiver.Fields
				.Select(f =>
				{
					SyntaxNode node = f;
					while (!(node is BaseTypeDeclarationSyntax) && node != null)
					{
						node = node.Parent;
					}

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
				if (context.Node is BaseTypeDeclarationSyntax type && !Types.Contains(type))
				{
					var fields = type.ChildNodes().OfType<FieldDeclarationSyntax>();

					foreach (var field in fields)
					{
						foreach (var variable in field.Declaration.Variables)
						{
							var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
							var attributes = fieldSymbol.GetAttributes();

						var names = attributes.Select(a => a.AttributeClass.ToDisplayString());
						var match = names.Contains(Synchronized.GeneratedType.Identifier.ToString()) ||
									names.Contains(SynchronizationAuthority.GeneratedType.Identifier.ToString());

						if (match)
						{
							Types.Add(type);
							return;
						}
					}
				}
			}
		}
	}
}
