using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace RhoMicro.CodeAnalysis
{
    public readonly struct TypeIdentifierName : IEquatable<TypeIdentifierName>
    {
        public readonly ImmutableArray<IdentifierPart> Parts;

        private TypeIdentifierName(ImmutableArray<IdentifierPart> parts)
        {
            Parts = parts;
        }

        public static TypeIdentifierName CreateAttribute<T>()
        {
            return CreateAttribute(typeof(T));
        }
        public static TypeIdentifierName CreateAttribute(Type type)
        {
            return Create().WithNamePart(Regex.Replace(type.Name, @"Attribute$", String.Empty));
        }
        public static TypeIdentifierName Create<T>()
        {
            return Create(typeof(T));
        }
        public static TypeIdentifierName Create(Type type)
        {
            return Create().WithNamePart(type.Name);
        }
        public static TypeIdentifierName Create(ITypeSymbol symbol)
        {
            var result = Create();

            if (symbol.ContainingType != null)
            {
                var containingType = Create(symbol.ContainingType);
                result = result.WithTypePart(containingType);
            }

            var flag = false;
            if (symbol is IArrayTypeSymbol arraySymbol)
            {
                flag = true;
                symbol = arraySymbol.ElementType;
            }

            result = result.WithNamePart(symbol.Name);

            if (symbol is INamedTypeSymbol namedSymbol && namedSymbol.TypeArguments.Any())
            {
                var arguments = new TypeIdentifier[namedSymbol.TypeArguments.Length];

                for (var i = 0; i < arguments.Length; i++)
                {
                    var typeArgument = namedSymbol.TypeArguments[i];
                    TypeIdentifier argument = default;
                    if (SymbolEqualityComparer.Default.Equals(typeArgument.ContainingType, namedSymbol))
                    {
                        argument = TypeIdentifier.Create(TypeIdentifierName.Create().WithNamePart(typeArgument.ToString()), Namespace.Create());
                    }
                    else
                    {
                        argument = TypeIdentifier.Create(typeArgument);
                    }

                    arguments[i] = argument;
                }

                result = result.WithGenericPart(arguments);
            }

            if (flag)
            {
                result = result.WithArrayPart();
            }

            return result;
        }
        public static TypeIdentifierName Create()
        {
            return new TypeIdentifierName(ImmutableArray<IdentifierPart>.Empty);
        }

        public TypeIdentifierName WithTypePart(TypeIdentifierName type)
        {
            var parts = GetNextParts(IdentifierPart.PartKind.Name)
                .AddRange(type.Parts);

            return new TypeIdentifierName(parts);
        }
        public TypeIdentifierName WithNamePart(String name)
        {
            var parts = GetNextParts(IdentifierPart.PartKind.Name)
                .Add(IdentifierPart.Name(name));

            return new TypeIdentifierName(parts);
        }
        public TypeIdentifierName WithGenericPart(TypeIdentifier[] arguments)
        {
            var parts = GetNextParts(IdentifierPart.PartKind.GenericOpen)
                .Add(IdentifierPart.GenericOpen());

            var typesArray = arguments ?? Array.Empty<TypeIdentifier>();

            for (var i = 0; i < typesArray.Length; i++)
            {
                var type = typesArray[i];

                if (type.Namespace.Parts.Any())
                {
                    parts = parts.AddRange(type.Namespace.Parts)
                                 .Add(IdentifierPart.Period());
                }

                parts = parts.AddRange(type.Name.Parts);

                if (i != typesArray.Length - 1)
                {
                    parts = parts.Add(IdentifierPart.Comma());
                }
            }

            parts = parts.Add(IdentifierPart.GenericClose());

            return new TypeIdentifierName(parts);
        }
        public TypeIdentifierName WithArrayPart()
        {
            var parts = GetNextParts(IdentifierPart.PartKind.Array).Add(IdentifierPart.Array());
            return new TypeIdentifierName(parts);
        }

        private ImmutableArray<IdentifierPart> GetNextParts(IdentifierPart.PartKind nextKind)
        {
            var lastKind = Parts.LastOrDefault().Kind;

            var prependSeparator = nextKind == IdentifierPart.PartKind.Name &&
                                        (lastKind == IdentifierPart.PartKind.GenericOpen ||
                                        lastKind == IdentifierPart.PartKind.Name);

            if (prependSeparator)
            {
                return Parts.Add(IdentifierPart.Period());
            }

            return Parts;
        }

        public override String ToString()
        {
            return String.Concat(Parts);
        }

        public override Boolean Equals(Object obj)
        {
            return obj is TypeIdentifierName name && Equals(name);
        }

        public Boolean Equals(TypeIdentifierName other)
        {
            return Parts.SequenceEqual(other.Parts);
        }

        public override Int32 GetHashCode()
        {
            return 666791821 + Parts.GetHashCode();
        }

        public static Boolean operator ==(TypeIdentifierName left, TypeIdentifierName right)
        {
            return left.Equals(right);
        }

        public static Boolean operator !=(TypeIdentifierName left, TypeIdentifierName right)
        {
            return !(left == right);
        }
    }
}
