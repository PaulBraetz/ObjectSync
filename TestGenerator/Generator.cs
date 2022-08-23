using Microsoft.CodeAnalysis;
using System;
using System.Threading;

namespace TestGenerator
{
	[Generator]
	public class Generator : ISourceGenerator
	{
		public void Execute(GeneratorExecutionContext context)
		{
			AddSource(context);
		}

		public void Initialize(GeneratorInitializationContext context)
		{

		}

		private static void AddSource(GeneratorExecutionContext context)
		{
			var mainType = context.Compilation.GetEntryPoint(CancellationToken.None).ContainingType;
			var source =
$@"
using System.ComponentModel;

namespace {mainType.ContainingNamespace}
{{
	{mainType.DeclaredAccessibility.ToString().ToLower()} partial class {mainType.Name}
	{{
		public event PropertyChangedEventHandler? PropertyChanged;
	}}
}}";
			var hintName = mainType.Name.ToString();

			context.AddSource(hintName, source);
		}
	}
}
