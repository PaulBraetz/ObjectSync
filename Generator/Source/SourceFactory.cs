using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObjectSync.Attributes;
using RhoMicro.CodeAnalysis;
using RhoMicro.CodeAnalysis.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.Mime;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ObjectSync.Generator
{
	internal sealed partial class SourceFactory
	{
		#region Aliae
		private TypeIdentifier TypeIdAttributeIdentifier => GeneratedAttributes.TypeId.GeneratedType.Identifier;
		private TypeIdentifier InstanceIdAttributeIdentifier => GeneratedAttributes.InstanceId.GeneratedType.Identifier;
		private TypeIdentifier SourceInstanceIdAttributeIdentifier => GeneratedAttributes.SourceInstanceId.GeneratedType.Identifier;
		private TypeIdentifier SynchronizationAuthorityAttributeIdentifier => GeneratedAttributes.SynchronizationAuthority.GeneratedType.Identifier;
		private TypeIdentifier SynchronizationTargetAttributeIdentifier => GeneratedAttributes.SynchronizationTarget.GeneratedType.Identifier;
		private TypeIdentifier SynchronizedAttributeIdentifier => GeneratedAttributes.Synchronized.GeneratedType.Identifier;

		private IAttributeFactory<TypeIdAttribute> TypeIdAttributeFactory => GeneratedAttributes.TypeId.Factory;
		private IAttributeFactory<InstanceIdAttribute> InstanceIdAttributeFactory => GeneratedAttributes.InstanceId.Factory;
		private IAttributeFactory<SourceInstanceIdAttribute> SourceInstanceIdAttributeFactory => GeneratedAttributes.SourceInstanceId.Factory;
		private IAttributeFactory<SynchronizationAuthorityAttribute> SynchronizationAuthorityAttributeFactory => GeneratedAttributes.SynchronizationAuthority.Factory;
		private IAttributeFactory<SynchronizationTargetAttribute> SynchronizationTargetAttributeFactory => GeneratedAttributes.SynchronizationTarget.Factory;
		private IAttributeFactory<SynchronizedAttribute> SynchronizedAttributeFactory => GeneratedAttributes.Synchronized.Factory;

		private TypeIdentifier ISynchronizationAuthorityIdentifier => GeneratedSynchronizationClasses.ISynchronizationAuthority.Identifier;
		#endregion

		private DeclaredInfo Declared { get; }
		private ContextFactory Context { get; }
		private MembersFactory Members { get; }

		private Optional<GeneratedSource> _generatedSource;

		public SourceFactory(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel)
		{
			Declared = new DeclaredInfo(synchronizedType, semanticModel, this);
			Members = new MembersFactory(this);
			Context = new ContextFactory(this);
		}

		public static GeneratedSource GetSource(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel)
		{
			var source = new SourceFactory(synchronizedType, semanticModel).GetSource();

			return source;
		}

		public GeneratedSource GetSource()
		{
			if (!_generatedSource.HasValue)
			{
				var name = Declared.TypeIdentifier;

				try
				{
					var source = GetNamespaceDeclaration();

					_generatedSource = new GeneratedSource(source, name);
				}
				catch (Exception ex)
				{
					var source =
$@"/*
An error occured while generating this source file for {Declared.TypeIdentifier}:
{ex}
*/";
					_generatedSource = new GeneratedSource(source, name);
				}
			}

			return _generatedSource.Value;
		}

		#region Type
		private NamespaceDeclarationSyntax GetNamespaceDeclaration()
		{
			var namespaceName = TryGetNamespace(Declared.Type, out var declaredNamespace) ?
				declaredNamespace.Name :
				throw new Exception($"{Declared.TypeIdentifier} was not declared in a namespace.");

			var generatedTypeDeclaration = GetGeneratedTypeDeclaration();

			var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(namespaceName)
					.AddMembers(generatedTypeDeclaration);

			return namespaceDeclaration;
		}
		private MemberDeclarationSyntax[] GetGeneratedTypeMembers()
		{
			var members = new MemberDeclarationSyntax[]
				{
					Context.GetDeclaration(),
					Members.ContextField,
					Members.Context,
					Members.TypeId,
					Members.SourceInstanceId,
					Members.InstanceId
				}.Concat(Members.GeneratedProperties)
				.Where(m => m != null)
				.OrderBy(m => m.GetType().Name)
				.ToArray();

			return members;
		}
		private BaseTypeDeclarationSyntax GetGeneratedTypeDeclaration()
		{
			var synchronizedTypeDeclarationName = Declared.TypeIdentifier.Name;
			var generatedTypeMembers = GetGeneratedTypeMembers();

			var generatedTypeDeclaration = SyntaxFactory.TypeDeclaration(SyntaxKind.ClassDeclaration, synchronizedTypeDeclarationName)
				.WithModifiers(Declared.Type.Modifiers)
				.WithMembers(new SyntaxList<MemberDeclarationSyntax>(generatedTypeMembers))
				.WithLeadingTrivia(
					SyntaxFactory.Trivia(
						SyntaxFactory.NullableDirectiveTrivia(
							SyntaxFactory.Token(SyntaxKind.RestoreKeyword),
							true)))
				.WithTrailingTrivia(
					SyntaxFactory.Trivia(
						SyntaxFactory.NullableDirectiveTrivia(
							SyntaxFactory.Token(SyntaxKind.DisableKeyword),
							true)));

			return generatedTypeDeclaration;
		}
		#endregion

		#region Misc
		private StatementSyntax GetSetStatement(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var propertyChangingCall = GetPropertyChangingCall(field, fromWithinContext);
			var fieldName = GetFieldName(field);
			var propertyChangedCall = GetPropertyChangedCall(field, fromWithinContext);

			var statement = SyntaxFactory.ParseStatement(
$@"{propertyChangingCall}
{(fromWithinContext ? $"{Context.InstancePropertyAccess}." : String.Empty)}{fieldName} = value;{propertyChangedCall}");

			return statement;
		}
		private StatementSyntax GetPropertyChangingCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var statement = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success)
						.attributeInstance?
						.Observable ?? false ?
				SyntaxFactory.ParseStatement($"{(fromWithinContext ? $"{Context.InstancePropertyAccess}." : String.Empty)}{Members.PropertyChangingEventMethodName}(\"{GetGeneratedPropertyName(field)}\");") :
				null;

			return statement;
		}
		private StatementSyntax GetPropertyChangedCall(FieldDeclarationSyntax field, Boolean fromWithinContext)
		{
			var statement = field.AttributeLists.SelectMany(al => al.Attributes)
						.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var attributeInstance), attributeInstance))
						.FirstOrDefault(t => t.success).attributeInstance?.Observable ?? false ?
				 SyntaxFactory.ParseStatement($"{(fromWithinContext ? $"{Context.InstancePropertyAccess}." : String.Empty)}{Members.PropertyChangedEventMethodName}(\"{GetGeneratedPropertyName(field)}\");") :
				null;

			return statement;
		}

		private String GetFieldName(FieldDeclarationSyntax field)
		{
			return field.Declaration.Variables.Single().Identifier.Text;
		}
		private TypeIdentifier GetFieldType(FieldDeclarationSyntax field)
		{
			var type = GetType(field.Declaration.Type);

			return type;
		}
		private TypeIdentifier GetType(TypeSyntax type)
		{
			var symbol = Declared.SemanticModel.GetDeclaredSymbol(type) as ITypeSymbol ?? Declared.SemanticModel.GetTypeInfo(type).Type;

			var identifier = TypeIdentifier.Create(symbol);

			return identifier;
		}
		private String GetGeneratedPropertyName(FieldDeclarationSyntax field)
		{
			var attributeInstance = field.AttributeLists.SelectMany(al => al.Attributes)
				.Select(a => (success: SynchronizedAttributeFactory.TryBuild(a, Declared.SemanticModel, out var instance), instance))
				.FirstOrDefault(t => t.success).instance;

			var propertyName = attributeInstance?.PropertyName;
			var isObservable = attributeInstance?.Observable ?? false;

			if (String.IsNullOrEmpty(propertyName))
			{
				var fieldName = GetFieldName(field);

				if (fieldName[0] == '_' || fieldName[0] == Char.ToLowerInvariant(fieldName[0]))
				{
					var sanitizedFieldName = Regex.Replace(fieldName, @"^_*", String.Empty);
					propertyName = String.Concat(Char.ToUpperInvariant(sanitizedFieldName[0]), sanitizedFieldName.Substring(1, sanitizedFieldName.Length - 1));
				}
				else if (isObservable)
				{
					propertyName = getPrefixedName(Members.ObservablePropertyPrefix);
				}
				else
				{
					propertyName = getPrefixedName(Members.SynchronizedPropertyPrefix);
				}

				String getPrefixedName(String prefix)
				{
					return String.Concat(prefix, Char.ToUpperInvariant(fieldName[0]), fieldName.Substring(1, fieldName.Length - 1));
				}
			}

			return propertyName;
		}
		private Boolean TryGetNamespace(SyntaxNode node, out BaseNamespaceDeclarationSyntax namespaceDeclaration)
		{
			while (node.Parent != null && !(node is BaseNamespaceDeclarationSyntax))
			{
				node = node.Parent;
			}

			namespaceDeclaration = node == null ?
				null :
				(node as BaseNamespaceDeclarationSyntax);

			return namespaceDeclaration != null;
		}

		private void ThrowIfMultiple<T>(T[] items, String declarationType, TypeIdentifier attribute)
		{
			if (items.Length > 1)
			{
				throw new Exception($"Multiple {declarationType} annotated with {attribute} have been declared in {Declared.TypeIdentifier}.");
			}
		}
		#endregion
	}
}
