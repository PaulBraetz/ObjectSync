using Microsoft.CodeAnalysis;

namespace ObjectSync.Generator
{
	[Generator]
	internal class Generator : ISourceGenerator
	{
		public void Execute(GeneratorExecutionContext context)
		{
			var extractor = new ObjectSyncSourceExtractor(context.Compilation);
			var sources = extractor.GetSources();
			context.AddSources(sources);
		}

		public void Initialize(GeneratorInitializationContext context)
		{

		}
	}
}
