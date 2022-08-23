using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestGenerator.Models
{
	internal readonly struct ModelExtractor
	{


		private readonly Compilation _compilation;

		public ModelExtractor(Compilation compilation)
		{
			_compilation = compilation;
		}


	}
}
