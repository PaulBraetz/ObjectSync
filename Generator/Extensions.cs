using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ObjectSync.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ObjectSync.Generator
{
	internal static class Extensions
	{
		public static SyntaxKind[] AsSyntax(this ObjectSync.Attributes.Attributes.Accessibility accessibility, SyntaxKind notApplicable = SyntaxKind.PrivateKeyword)
		{
			SyntaxKind[] kinds;

			switch (accessibility)
			{
				case ObjectSync.Attributes.Attributes.Accessibility.Protected:
					kinds = new SyntaxKind[] { SyntaxKind.ProtectedKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Accessibility.Public:
					kinds = new SyntaxKind[] { SyntaxKind.PublicKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Accessibility.Internal:
					kinds = new SyntaxKind[] { SyntaxKind.InternalKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Accessibility.ProtectedOrInternal:
					kinds = new SyntaxKind[] { SyntaxKind.InternalKeyword, SyntaxKind.ProtectedKeyword };
					break;
				default:
					kinds = new SyntaxKind[] { notApplicable };
					break;
			}

			return kinds;
		}
		public static SyntaxKind[] AsSyntax(this ObjectSync.Attributes.Attributes.Modifier modifier, SyntaxKind notApplicable = SyntaxKind.None)
		{
			SyntaxKind[] kinds;

			switch (modifier)
			{
				case ObjectSync.Attributes.Attributes.Modifier.New:
					kinds = new SyntaxKind[] { SyntaxKind.NewKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Modifier.Override:
					kinds = new SyntaxKind[] { SyntaxKind.OverrideKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Modifier.Virtual:
					kinds = new SyntaxKind[] { SyntaxKind.VirtualKeyword };
					break;
				case ObjectSync.Attributes.Attributes.Modifier.Sealed:
					kinds = new SyntaxKind[] { SyntaxKind.SealedKeyword, SyntaxKind.OverrideKeyword };
					break;
				default:
					kinds = new SyntaxKind[] { notApplicable };
					break;
			}

			return kinds;
		}
		public static SyntaxTriviaList AsLeadingTrivia(this String text)
		{
			if (!text.EndsWith("\n"))
			{
				text += "\n";
			}

			var comments = SyntaxFactory.ParseLeadingTrivia(text);

			return comments;
		}
	}
}
