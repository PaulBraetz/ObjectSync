using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RhoMicro.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using static ObjectSync.Generator.GeneratedAttributes;

namespace ObjectSync.Generator
{
	[Generator]
	internal class Generator : ISourceGenerator
	{
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

					return node;
				})
				.Where(t => t != null)
				.OfType<BaseTypeDeclarationSyntax>()
				.Distinct()
				.Select(d => PartialTypeSource.GetSource(d, context.Compilation.GetSemanticModel(d.SyntaxTree)));

			context.AddSources(sources);
		}

		public void Initialize(GeneratorInitializationContext context)
		{
			var sources = new[] { TypeId.GeneratedType, SourceInstanceId.GeneratedType, InstanceId.GeneratedType, Synchronized.GeneratedType, SynchronizationAuthority.GeneratedType }
			.Select(t => t.Source)
			.ToArray();
			context.RegisterForPostInitialization(c => c.AddSources(sources));

			context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
		}
		private sealed class SyntaxReceiver : ISyntaxContextReceiver
		{
			public List<FieldDeclarationSyntax> Fields { get; } = new List<FieldDeclarationSyntax>();

			public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
			{
				if (context.Node is FieldDeclarationSyntax field)
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
							Fields.Add(field);
						}
					}
				}
			}
		}
	}
}
