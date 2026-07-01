using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ZeroPlus.Oms.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class BindablePropertyGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor FieldHasInitializerDescriptor = new(
        id: "BIND001",
        title: "Bindable property has backing field with inline initializer",
        messageFormat: "Property '{0}' is [Bindable] but its backing field '{1}' has an inline initializer; use [Bindable(Default = ...)] or [Bindable(Initialize = true)], or set the value in the constructor",
        category: "ZeroPlusGenerators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PropertyHasInitializerDescriptor = new(
        id: "BIND002",
        title: "Bindable property should not have an inline initializer",
        messageFormat: "Property '{0}' is [Bindable] and should not have an inline initializer; use [Bindable(Default = ...)] or [Bindable(Initialize = true)], or set the value in the constructor",
        category: "ZeroPlusGenerators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("BindableAttribute.g.cs", BindableAttributeSource.Source));

        var properties = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                BindableAttributeSource.FullyQualifiedName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, ct) => GetPropertyInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var collected = properties.Collect();

        context.RegisterSourceOutput(collected, static (spc, props) =>
        {
            // Group by MetadataIdentity only: ClassKey (incl. ImmutableArray<ClassLayer>) can differ for the same
            // INamedTypeSymbol across incremental steps, which would emit duplicate hint names for one type.
            var groups = props.GroupBy(p => p.ContainingClassKey.MetadataIdentity);
            foreach (var group in groups)
            {
                var key = group.First().ContainingClassKey;
                GenerateSource(spc, new BindableClassGenInfo(key, group.ToImmutableArray()));
            }
        });

        var fieldInitializerDiags = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                BindableAttributeSource.FullyQualifiedName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, ct) => CheckForFieldInitializer(ctx))
            .Where(static d => d is not null);

        context.RegisterSourceOutput(fieldInitializerDiags, static (spc, diag) =>
        {
            var d = diag!.Value;
            spc.ReportDiagnostic(Diagnostic.Create(FieldHasInitializerDescriptor, d.Location, d.PropertyName, d.FieldName));
        });

        var propInitializerDiags = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is PropertyDeclarationSyntax { Initializer: not null } prop &&
                HasBindableAttribute(prop),
            transform: static (ctx, ct) =>
            {
                var prop = (PropertyDeclarationSyntax)ctx.Node;
                return (PropertyName: prop.Identifier.Text, Location: prop.Initializer!.GetLocation());
            });

        context.RegisterSourceOutput(propInitializerDiags, static (spc, diag) =>
        {
            spc.ReportDiagnostic(Diagnostic.Create(PropertyHasInitializerDescriptor, diag.Location, diag.PropertyName));
        });
    }

    private static bool HasBindableAttribute(PropertyDeclarationSyntax prop)
    {
        foreach (var attrList in prop.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name switch
                {
                    IdentifierNameSyntax id => id.Identifier.Text,
                    QualifiedNameSyntax q => q.Right.Identifier.Text,
                    _ => attr.Name.ToString()
                };
                if (name is "Bindable" or "BindableAttribute")
                    return true;
            }
        }
        return false;
    }

    private static BindablePropertyInfo? GetPropertyInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol propertySymbol)
            return null;

        if (propertySymbol.ContainingType is not { } containingType)
            return null;

        var (backingFieldName, hasExistingField) = ResolveBackingFieldName(propertySymbol.Name, containingType);

        string? defaultExpression = null;
        if (!hasExistingField && ctx.Attributes.Length > 0)
        {
            var attr = ctx.Attributes[0];
            var initialize = false;

            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Default")
                    defaultExpression = FormatTypedConstant(namedArg.Value);
                else if (namedArg.Key == "Initialize" && namedArg.Value.Value is true)
                    initialize = true;
            }

            if (defaultExpression is null && initialize)
                defaultExpression = "new()";
        }

        var propAccessibility = AccessibilityToString(propertySymbol.DeclaredAccessibility);

        return new BindablePropertyInfo(
            PropertyName: propertySymbol.Name,
            PropertyType: propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            PropertyAccessibility: propAccessibility,
            BackingFieldName: backingFieldName,
            HasExistingField: hasExistingField,
            DefaultExpression: defaultExpression,
            ContainingClassKey: ClassKeyFactory.Create(containingType));
    }

    private static (string PropertyName, string FieldName, Location Location)? CheckForFieldInitializer(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol prop)
            return null;
        if (prop.ContainingType is not { } type)
            return null;

        var camel = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";
        var pascal = $"_{prop.Name}";

        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsReadOnly)
                continue;
            if (field.Name != camel && field.Name != pascal)
                continue;

            foreach (var syntaxRef in field.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null } declarator)
                    return (prop.Name, field.Name, declarator.GetLocation());
            }
        }
        return null;
    }

    private static (string Name, bool Exists) ResolveBackingFieldName(string propertyName, INamedTypeSymbol containingType)
    {
        var camelCase = $"_{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";
        var pascalCase = $"_{propertyName}";

        var camelTaken = false;
        var pascalTaken = false;

        foreach (var member in containingType.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                if (field.Name == camelCase)
                {
                    if (!field.IsReadOnly)
                        return (camelCase, true);
                    camelTaken = true;
                }
                if (field.Name == pascalCase)
                {
                    if (!field.IsReadOnly)
                        return (pascalCase, true);
                    pascalTaken = true;
                }
            }
        }

        if (!camelTaken)
            return (camelCase, false);
        if (!pascalTaken)
            return (pascalCase, false);

        return (camelCase, false);
    }

    private static string AccessibilityToString(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        _ => "internal"
    };

    private static string? FormatTypedConstant(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Error)
            return null;

        if (constant.IsNull)
            return "null";

        if (constant.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            var fqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol field && field.HasConstantValue && Equals(field.ConstantValue, constant.Value))
                    return $"{fqn}.{field.Name}";
            }
            return $"({fqn}){constant.Value}";
        }

        var value = constant.Value;
        if (value is bool b) return b ? "true" : "false";
        if (value is string s) return $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        if (value is char c) return $"'{c}'";
        if (value is double d)
        {
            if (double.IsNaN(d)) return "double.NaN";
            if (double.IsPositiveInfinity(d)) return "double.PositiveInfinity";
            if (double.IsNegativeInfinity(d)) return "double.NegativeInfinity";
            var str = d.ToString("R");
            if (!str.Contains(".") && !str.Contains("E")) str += ".0";
            return str;
        }
        if (value is float f)
        {
            if (float.IsNaN(f)) return "float.NaN";
            if (float.IsPositiveInfinity(f)) return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(f)) return "float.NegativeInfinity";
            return f.ToString("R") + "f";
        }
        if (value is long l) return l.ToString() + "L";
        if (value is uint u) return u.ToString() + "U";
        if (value is ulong ul) return ul.ToString() + "UL";
        if (value is decimal m) return m.ToString() + "m";

        return value?.ToString();
    }

    private static void GenerateSource(SourceProductionContext spc, BindableClassGenInfo classInfo)
    {
        var key = classInfo.Key;
        var props = classInfo.Properties;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable CS8625");
        sb.AppendLine();

        NestedPartialClassEmitter.AppendNamespace(sb, key.Namespace);
        NestedPartialClassEmitter.AppendClassOpenings(sb, key);

        var mi = NestedPartialClassEmitter.MemberIndent(key);

        foreach (var prop in props)
        {
            var fieldName = prop.BackingFieldName;

            if (!prop.HasExistingField)
            {
                if (prop.DefaultExpression is not null)
                    sb.AppendLine($"{mi}private {prop.PropertyType} {fieldName} = {prop.DefaultExpression};");
                else
                    sb.AppendLine($"{mi}private {prop.PropertyType} {fieldName};");
                sb.AppendLine();
            }

            sb.AppendLine($"{mi}{prop.PropertyAccessibility} partial {prop.PropertyType} {prop.PropertyName}");
            sb.AppendLine($"{mi}{{");
            sb.AppendLine($"{mi}    get => {fieldName};");
            sb.AppendLine($"{mi}    set");
            sb.AppendLine($"{mi}    {{");
            sb.AppendLine($"{mi}        Coerce{prop.PropertyName}(ref value);");
            sb.AppendLine($"{mi}        SetValue(ref {fieldName}, value, () => On{prop.PropertyName}Changed(value));");
            sb.AppendLine($"{mi}    }}");
            sb.AppendLine($"{mi}}}");
            sb.AppendLine();
        }

        foreach (var prop in props)
        {
            sb.AppendLine($"{mi}partial void Coerce{prop.PropertyName}(ref {prop.PropertyType} value);");
            sb.AppendLine($"{mi}partial void On{prop.PropertyName}Changed({prop.PropertyType} value);");
        }

        NestedPartialClassEmitter.AppendClassClosings(sb, key);

        var fileStem = ClassKeyFactory.SanitizeForFileName(key.MetadataIdentity);
        spc.AddSource($"{fileStem}.BindableProperties.g.cs", sb.ToString());
    }
}

internal readonly record struct BindablePropertyInfo(
    string PropertyName,
    string PropertyType,
    string PropertyAccessibility,
    string BackingFieldName,
    bool HasExistingField,
    string? DefaultExpression,
    ClassKey ContainingClassKey);

internal readonly record struct BindableClassGenInfo(
    ClassKey Key,
    ImmutableArray<BindablePropertyInfo> Properties);
