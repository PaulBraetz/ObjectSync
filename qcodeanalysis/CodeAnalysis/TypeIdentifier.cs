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
			var @namespace = Namespace.Create();

			if (type.IsNested)
			{
				var parentType = type.DeclaringType;
				var parentTypeIdentifier = Create(parentType);
				name = name.AppendTypePart(parentTypeIdentifier.Name);
				@namespace = parentTypeIdentifier.Namespace;
			}

			var typeName = type.Name;
			if (type.IsGenericType)
			{
				var iBacktick = typeName.IndexOf('`');
				if (iBacktick > 0)
				{
					typeName = typeName.Remove(iBacktick);
				}
			}

			name = name.AppendNamePart(typeName);

			if (type.IsConstructedGenericType)
			{
				var genericArguments = type.GenericTypeArguments.Select(Create).ToArray();
				name = name.AppendGenericPart(genericArguments);
			}

			if (type.IsArray)
			{
				name = name.AppendArrayPart();
			}

			if (@namespace == default)
			{
				@namespace = Namespace.Create(type);
			}

			return Create(name, @namespace);
		}
		public static TypeIdentifier Create(ITypeSymbol symbol)
		{
			var identifier = symbol is ITypeParameterSymbol parameter?
				 TypeIdentifierName.Create().AppendNamePart(parameter.Name):
				 TypeIdentifierName.Create(symbol);
			var @namespace = symbol is ITypeParameterSymbol?
				Namespace.Create():
				Namespace.Create(symbol);

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

		public static implicit operator String(TypeIdentifier identifier)
		{
			return identifier.ToString();
		}
	}
}
