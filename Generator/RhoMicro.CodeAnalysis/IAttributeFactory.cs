using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal interface IAttributeFactory<T>
	{
		Boolean TryBuild(AttributeSyntax attributeData, SemanticModel semanticModel, out T attribute);
	}
}
