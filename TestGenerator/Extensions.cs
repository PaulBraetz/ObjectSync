
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestGenerator
{
	internal static class Extensions
	{
		public static void AddSource(this GeneratorExecutionContext context, GeneratedSource source)
		{
			context.AddSource(source.HintName, source.Source);
		}
		public static void AddSources(this GeneratorExecutionContext context, IEnumerable<GeneratedSource> sources)
		{
			foreach(var source in sources)
			{
				context.AddSource(source);
			}
		}
	}
}
