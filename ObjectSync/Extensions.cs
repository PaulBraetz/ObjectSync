using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RhoMicro.CodeAnalysis;
using RhoMicro.ObjectSync.Attributes;

using System;

using Accessibility = RhoMicro.ObjectSync.Attributes.Accessibility;

namespace RhoMicro.ObjectSync
{
    internal static class Extensions
    {
        public static SyntaxKind[] AsSyntax(this Accessibility accessibility, SyntaxKind notApplicable = SyntaxKind.PrivateKeyword)
        {
            SyntaxKind[] kinds;

            switch(accessibility)
            {
                case Accessibility.Protected:
                    kinds = new SyntaxKind[] { SyntaxKind.ProtectedKeyword };
                    break;
                case Accessibility.Public:
                    kinds = new SyntaxKind[] { SyntaxKind.PublicKeyword };
                    break;
                case Accessibility.Internal:
                    kinds = new SyntaxKind[] { SyntaxKind.InternalKeyword };
                    break;
                case Accessibility.ProtectedOrInternal:
                    kinds = new SyntaxKind[] { SyntaxKind.InternalKeyword, SyntaxKind.ProtectedKeyword };
                    break;
                default:
                    kinds = notApplicable != SyntaxKind.None ?
                        new SyntaxKind[] { notApplicable } :
                        Array.Empty<SyntaxKind>();
                    break;
            }

            return kinds;
        }
        public static SyntaxKind[] AsSyntax(this Modifier modifier, SyntaxKind notApplicable = SyntaxKind.None)
        {
            SyntaxKind[] kinds;

            switch(modifier)
            {
                case Modifier.New:
                    kinds = new SyntaxKind[] { SyntaxKind.NewKeyword };
                    break;
                case Modifier.Override:
                    kinds = new SyntaxKind[] { SyntaxKind.OverrideKeyword };
                    break;
                case Modifier.Virtual:
                    kinds = new SyntaxKind[] { SyntaxKind.VirtualKeyword };
                    break;
                case Modifier.Sealed:
                    kinds = new SyntaxKind[] { SyntaxKind.SealedKeyword, SyntaxKind.OverrideKeyword };
                    break;
                default:
                    kinds = notApplicable != SyntaxKind.None ?
                        new SyntaxKind[] { notApplicable } :
                        Array.Empty<SyntaxKind>();
                    break;
            }

            return kinds;
        }
        public static SyntaxTriviaList AsLeadingTrivia(this String text)
        {
            if(!text.EndsWith("\r\n"))
            {
                text += "\r\n";
            }

            var comments = SyntaxFactory.ParseLeadingTrivia(text);

            return comments;
        }
        public static TypeIdentifier GetSynchronizationType<T>(this TypeExportConfigurationAttribute config) => GetSynchronizationType(config, TypeIdentifierName.Create<T>());
        public static TypeIdentifier GetSynchronizationType(this TypeExportConfigurationAttribute config, TypeIdentifierName name)
        {
            var @namespace = config.GetSynchronizationNamespace();
            var identifier = TypeIdentifier.Create(name, @namespace);

            return identifier;
        }
        public static Namespace GetSynchronizationNamespace(this TypeExportConfigurationAttribute config)
        {
            var @namespace = Namespace.Create();

            if(!String.IsNullOrWhiteSpace(config.RootNamespace))
            {
                @namespace = @namespace.Append(config.RootNamespace);
            }

            @namespace = @namespace
                .Append("ObjectSync")
                .Append("Synchronization");

            return @namespace;
        }
    }
}
