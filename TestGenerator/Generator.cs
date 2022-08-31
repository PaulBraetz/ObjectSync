using Microsoft.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace TestGenerator
{
	[Generator]
	public class Generator : ISourceGenerator
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
