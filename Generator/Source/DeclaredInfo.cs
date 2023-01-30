using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RhoMicro.CodeAnalysis;
using RhoMicro.ObjectSync.Attributes;

using System;
using System.Linq;

namespace RhoMicro.ObjectSync.Generator
{
    internal sealed partial class SourceFactory
    {
        private sealed class DeclaredInfo
        {
            private readonly SourceFactory _parent;

            #region Properties
            public TypeExportConfigurationAttribute ExportConfig
            {
                get;
            }
            public BaseTypeDeclarationSyntax Type
            {
                get;
            }
            public SemanticModel SemanticModel
            {
                get;
            }

            private Optional<TypeIdentifier> _typeIdentifier;
            public TypeIdentifier TypeIdentifier
            {
                get
                {
                    if(!_typeIdentifier.HasValue)
                    {
                        _typeIdentifier = TypeIdentifier.Create(SemanticModel.GetDeclaredSymbol(Type));
                    }

                    return _typeIdentifier.Value;
                }
            }

            private SynchronizationTargetAttribute _synchronizationTargetAttribute;
            public SynchronizationTargetAttribute SynchronizationTargetAttribute
            {
                get
                {
                    if(_synchronizationTargetAttribute == null)
                    {
                        _synchronizationTargetAttribute = Type.AttributeLists
                                .OfAttributeClasses(SemanticModel, SynchronizationTargetAttributeIdentifier)
                                .Select(a => (success: SynchronizationTargetAttributeFactory.TryBuild(a, SemanticModel, out var attribute), attribute))
                                .Where(t => t.success)
                                .Select(t => t.attribute)
                                .Single();
                    }

                    return _synchronizationTargetAttribute;
                }
            }

            private TypeSyntax _typeSyntax;
            public TypeSyntax TypeSyntax
            {
                get
                {
                    if(_typeSyntax == null)
                    {
                        _typeSyntax = SyntaxFactory.ParseTypeName(TypeIdentifier);
                    }

                    return _typeSyntax;
                }
            }

            private FieldDeclarationSyntax[] _synchronizedFields;
            public FieldDeclarationSyntax[] SynchronizedFields => _synchronizedFields ?? (_synchronizedFields = Type.ChildNodes().OfType<FieldDeclarationSyntax>().Where(f => f.AttributeLists.HasAttributes(SemanticModel, SynchronizedAttributeIdentifier)).ToArray());

            private PropertyDeclarationSyntax[] _properties;
            public PropertyDeclarationSyntax[] Properties => _properties ?? (_properties = Type.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray());

            private PropertyDeclarationSyntax _typeAuthority;
            public PropertyDeclarationSyntax Authority
            {
                get
                {
                    if(_typeAuthority == null)
                    {
                        var authorityProperties = Properties
                            .Where(p => p.AttributeLists.HasAttributes(SemanticModel, SynchronizationAuthorityAttributeIdentifier))
                            .ToArray();

                        if(authorityProperties.Length > 1)
                        {
                            throw new Exception($"{TypeIdentifier} cannot provide multiple synchronization authorities.");
                        }

                        _typeAuthority = authorityProperties.SingleOrDefault();
                    }

                    return _typeAuthority;
                }
            }

            private PropertyDeclarationSyntax _typeId;
            public PropertyDeclarationSyntax TypeId
            {
                get
                {
                    if(_typeId == null)
                    {
                        var typeIdProperties = Properties
                            .Where(p => p.AttributeLists.HasAttributes(SemanticModel, TypeIdAttributeIdentifier))
                            .ToArray();

                        if(typeIdProperties.Length > 1)
                        {
                            throw new Exception($"{TypeIdentifier} cannot provide multiple type ids.");
                        }

                        _typeId = typeIdProperties.SingleOrDefault();
                    }

                    return _typeId;
                }
            }

            private PropertyDeclarationSyntax _typeSourceInstanceId;
            public PropertyDeclarationSyntax SourceInstanceId
            {
                get
                {
                    if(_typeSourceInstanceId == null)
                    {
                        var sourceInstanceIdProperties = Properties
                            .Where(p => p.AttributeLists.HasAttributes(SemanticModel, SourceInstanceIdAttributeIdentifier))
                            .ToArray();

                        if(sourceInstanceIdProperties.Length > 1)
                        {
                            throw new Exception($"{TypeIdentifier} cannot provide multiple source instance ids.");
                        }

                        _typeSourceInstanceId = sourceInstanceIdProperties.SingleOrDefault();
                    }

                    return _typeSourceInstanceId;
                }
            }

            private PropertyDeclarationSyntax _instanceId;
            public PropertyDeclarationSyntax InstanceId
            {
                get
                {
                    if(_instanceId == null)
                    {
                        var instanceIdProperties = Properties
                            .Where(p => p.AttributeLists.HasAttributes(SemanticModel, InstanceIdAttributeIdentifier))
                            .ToArray();

                        if(instanceIdProperties.Length > 1)
                        {
                            throw new Exception($"{TypeIdentifier} cannot provide multiple instance ids.");
                        }

                        _instanceId = instanceIdProperties.SingleOrDefault();
                    }

                    return _instanceId;
                }
            }
            #endregion

            public DeclaredInfo(BaseTypeDeclarationSyntax synchronizedType, SemanticModel semanticModel, TypeExportConfigurationAttribute exportConfig, SourceFactory parent)
            {
                ExportConfig = exportConfig ?? throw new ArgumentNullException(nameof(exportConfig));
                Type = synchronizedType ?? throw new ArgumentNullException(nameof(synchronizedType));
                SemanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            }
        }
    }
}
