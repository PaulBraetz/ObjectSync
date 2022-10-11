using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal sealed class AttributeAnalysisUnit<T>
		where T : Attribute
	{
		public AttributeAnalysisUnit(String source)
		{
			Factory = AttributeFactory<T>.Create();
			GeneratedType = new GeneratedType(TypeIdentifier.Create<T>(), source);
		}
		public AttributeAnalysisUnit(SyntaxNode source)
		{
			Factory = AttributeFactory<T>.Create();
			GeneratedType = new GeneratedType(TypeIdentifier.Create<T>(), source);
		}

		public IAttributeFactory<T> Factory { get; }
		public GeneratedType GeneratedType { get; }
	}
}
