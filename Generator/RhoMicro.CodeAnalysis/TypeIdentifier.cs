using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace RhoMicro.CodeAnalysis
{
	internal readonly struct TypeIdentifier : IEquatable<TypeIdentifier>
	{
		public readonly TypeIdentifierName Name;
		public readonly Namespace Namespace;

		private TypeIdentifier(TypeIdentifierName name, Namespace @namespace)
		{
			Name = name;
			Namespace = @namespace;
		}

		public static TypeIdentifier Create<T>()
		{
			return Create(typeof(T));
		}
		public static TypeIdentifier Create(Type type)
		{
			var name = TypeIdentifierName.Create();
			Namespace @namespace = default;

			if (type.IsNested)
			{
				var parentType = type.Assembly.GetTypes().Single(t => t.GetNestedType(type.FullName) != null);
				var parentTypeIdentifier = Create(parentType);
				name = name.WithTypePart(parentTypeIdentifier.Name);
				@namespace = parentTypeIdentifier.Namespace;
			}

			name = name.WithNamePart(type.Name);

			if (type.IsConstructedGenericType)
			{
				var genericArguments = type.GenericTypeArguments.Select(Create).ToArray();
				name = name.WithGenericPart(genericArguments);
			}

			if (type.IsArray)
			{
				name = name.WithArrayPart();
			}

			if (@namespace == default)
			{
				@namespace = Namespace.Create(type);
			}

			return Create(name, @namespace);

		}
		public static TypeIdentifier Create(ITypeSymbol symbol)
		{
			var identifier = TypeIdentifierName.Create(symbol);
			var @namespace = Namespace.Create(symbol);

			return Create(identifier, @namespace);
		}
		public static TypeIdentifier Create(TypeIdentifierName name, Namespace @namespace)
		{
			return new TypeIdentifier(name, @namespace);
		}

		public override Boolean Equals(Object obj)
		{
			return obj is TypeIdentifier identifier && Equals(identifier);
		}

		public Boolean Equals(TypeIdentifier other)
		{
			return Name.Equals(other.Name) &&
				   Namespace.Equals(other.Namespace);
		}

		public override Int32 GetHashCode()
		{
			var hashCode = -179327946;
			hashCode = hashCode * -1521134295 + Name.GetHashCode();
			hashCode = hashCode * -1521134295 + Namespace.GetHashCode();
			return hashCode;
		}

		public override String ToString()
		{
			var namespaceString = Namespace.ToString();
			var nameString = Name.ToString();
			return String.IsNullOrEmpty(namespaceString) ? String.IsNullOrEmpty(nameString) ? null : nameString.ToString() : $"{namespaceString}.{nameString}";
		}

		public static Boolean operator ==(TypeIdentifier left, TypeIdentifier right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(TypeIdentifier left, TypeIdentifier right)
		{
			return !(left == right);
		}
	}
}
