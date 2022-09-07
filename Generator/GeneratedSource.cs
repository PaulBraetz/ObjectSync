﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ObjectSync.Generator
{
	internal readonly struct GeneratedSource : IEquatable<GeneratedSource>
	{
		public readonly String Source;
		public readonly String HintName;

		public GeneratedSource(String source, String className)
		{
			source =
$@"// <auto-generated/>
// {DateTimeOffset.Now}
{source}";

			Source = CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();

			HintName = $"{className}.g.cs";
		}

		public override Boolean Equals(Object obj)
		{
			return obj is GeneratedSource source && Equals(source);
		}

		public Boolean Equals(GeneratedSource other)
		{
			return Source == other.Source &&
				   HintName == other.HintName;
		}

		public override Int32 GetHashCode()
		{
			var hashCode = 854157587;
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(Source);
			hashCode = hashCode * -1521134295 + EqualityComparer<String>.Default.GetHashCode(HintName);
			return hashCode;
		}

		public static Boolean operator ==(GeneratedSource left, GeneratedSource right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(GeneratedSource left, GeneratedSource right)
		{
			return !(left == right);
		}

		public override String ToString()
		{
			return Source;
		}
	}
}