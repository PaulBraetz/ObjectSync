using System;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal sealed class AttributeAnalysisUnit<T>
		where T : Attribute
	{
		public AttributeAnalysisUnit(String sourceText)
		{
			Factory = AttributeFactory<T>.Create();
			GeneratedType = new GeneratedType(TypeIdentifier.Create<T>(), sourceText);
		}

		public IAttributeFactory<T> Factory { get; }
		public GeneratedType GeneratedType { get; }
	}
}
