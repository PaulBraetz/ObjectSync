using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal sealed class AttributeFactoryCollection<T> : IAttributeFactory<T>
	{
		private readonly List<IAttributeFactory<T>> _factories = new List<IAttributeFactory<T>>();

		public AttributeFactoryCollection<T> Add(IAttributeFactory<T> factory)
		{
			_factories.Add(factory);
			return this;
		}

		public Boolean TryBuild(AttributeSyntax attributeData, SemanticModel semanticModel, out T attribute)
		{
			foreach (var factory in _factories)
			{
				if (factory.TryBuild(attributeData, semanticModel, out var builtAttribute))
				{
					attribute = builtAttribute;
					return true;
				}
			}

			attribute = default;
			return false;
		}
	}
}
