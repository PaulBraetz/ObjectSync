using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace RhoMicro.CodeAnalysis
{
	internal readonly struct Namespace : IEquatable<Namespace>
	{
		private Namespace(ImmutableArray<IdentifierPart> parts)
		{
			Parts = parts;
		}

		public readonly ImmutableArray<IdentifierPart> Parts;

		public static Namespace Create<T>()
		{
			return Create(typeof(T));
		}
		public static Namespace Create(Type type)
		{
			var namespaceParts = type.Namespace.Split('.');
			return Create().AppendRange(namespaceParts);
		}
		public static Namespace Create(ISymbol symbol)
		{
			var result = Create();

			while (symbol != null && symbol.Name != String.Empty)
			{
				if (symbol is INamespaceSymbol)
				{
					result = result.Prepend(symbol.Name);
				}

				symbol = symbol.ContainingNamespace;
			}

			return result;
		}
		public static Namespace Create()
		{
			return new Namespace(ImmutableArray.Create<IdentifierPart>());
		}

		public Namespace Append(String name)
		{
			if (String.IsNullOrWhiteSpace(name))
			{
				return this;
			}

			var parts = GetNextParts().Add(IdentifierPart.Name(name));

			return new Namespace(parts);
		}
		public Namespace Prepend(String name)
		{
			if (String.IsNullOrWhiteSpace(name))
			{
				return this;
			}

			var parts = GetPreviousParts().Insert(0, IdentifierPart.Name(name));

			return new Namespace(parts);
		}
		public Namespace PrependRange(IEnumerable<String> names)
		{
			var @namespace = this;
			foreach (var name in names)
			{
				@namespace = @namespace.Prepend(name);
			}

			return @namespace;
		}
		public Namespace AppendRange(IEnumerable<String> names)
		{
			var @namespace = this;
			foreach (var name in names)
			{
				@namespace = @namespace.Append(name);
			}

			return @namespace;
		}

		private ImmutableArray<IdentifierPart> GetNextParts()
		{
			var lastKind = Parts.LastOrDefault().Kind;

			var prependSeparator = lastKind == IdentifierPart.PartKind.Name;

			return prependSeparator ?
				Parts.Add(IdentifierPart.Period()) :
				Parts;
		}
		private ImmutableArray<IdentifierPart> GetPreviousParts()
		{
			var firstKind = Parts.FirstOrDefault().Kind;

			var appendSeparator = firstKind == IdentifierPart.PartKind.Name;

			return appendSeparator ?
				Parts.Insert(0, IdentifierPart.Period()) :
				Parts;
		}

		public override String ToString()
		{
			return String.Concat(Parts);
		}

		public override Boolean Equals(Object obj)
		{
			return obj is Namespace @namespace && Equals(@namespace);
		}

		public Boolean Equals(Namespace other)
		{
			return Parts.IsDefaultOrEmpty ?
				other.Parts.IsDefaultOrEmpty :
				!other.Parts.IsDefaultOrEmpty && Parts.SequenceEqual(other.Parts);
		}

		public override Int32 GetHashCode()
		{
			return 666791821 + Parts.GetHashCode();
		}

		public static Boolean operator ==(Namespace left, Namespace right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(Namespace left, Namespace right)
		{
			return !(left == right);
		}

		public static implicit operator String(Namespace @namespace)
		{
			return @namespace.ToString();
		}
	}
}
