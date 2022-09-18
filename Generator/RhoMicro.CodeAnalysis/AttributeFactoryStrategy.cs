using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal sealed class AttributeFactoryStrategy<T> : AttributeFactory<T>
	{
		public AttributeFactoryStrategy(Func<AttributeSyntax, SemanticModel, Boolean> canBuildStrategy, Func<AttributeSyntax, SemanticModel, T> buildStrategy)
		{
			_buildStrategy = buildStrategy ?? throw new ArgumentNullException(nameof(buildStrategy));
			_canBuildStrategy = canBuildStrategy ?? throw new ArgumentNullException(nameof(canBuildStrategy));
		}

		private readonly Func<AttributeSyntax, SemanticModel, T> _buildStrategy;
		private readonly Func<AttributeSyntax, SemanticModel, bool> _canBuildStrategy;

		protected override Boolean CanBuild(AttributeSyntax attributeData, SemanticModel semanticModel)
		{
			return _canBuildStrategy.Invoke(attributeData, semanticModel);
		}
		protected override T Build(AttributeSyntax attributeData, SemanticModel semanticModel)
		{
			return _buildStrategy.Invoke(attributeData, semanticModel);
		}
	}
}
