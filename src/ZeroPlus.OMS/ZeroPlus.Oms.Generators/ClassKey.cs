using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace ZeroPlus.Oms.Generators;

internal readonly record struct ClassLayer(
    string Accessibility,
    string Modifiers,
    string ClassName);

/// <summary>
/// Identifies a declaring type for grouped source generation. Uses <see cref="MetadataIdentity"/>
/// so nested types and types with the same short name do not collide.
/// </summary>
internal readonly record struct ClassKey(
    string? Namespace,
    ImmutableArray<ClassLayer> ClassLayers,
    string MetadataIdentity);

internal static class ClassKeyFactory
{
    public static ClassKey Create(INamedTypeSymbol containingType)
    {
        var namespaceName = containingType.ContainingNamespace.IsGlobalNamespace
            ? null
            : containingType.ContainingNamespace.ToDisplayString();

        var layers = ImmutableArray.CreateBuilder<ClassLayer>();
        for (var t = containingType; t is not null; t = t.ContainingType)
        {
            layers.Add(new ClassLayer(
                AccessibilityToString(t.DeclaredAccessibility),
                GetClassModifiers(t),
                t.Name));
        }
        layers.Reverse();

        var identity = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new ClassKey(namespaceName, layers.ToImmutable(), identity);
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

    private static string GetClassModifiers(INamedTypeSymbol type)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (type.IsAbstract && type.TypeKind != TypeKind.Interface)
            parts.Add("abstract");
        if (type.IsSealed && !type.IsValueType)
            parts.Add("sealed");
        if (type.IsStatic)
            parts.Add("static");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Filesystem-safe, unique hint name for <see cref="Microsoft.CodeAnalysis.SourceProductionContext.AddSource"/>.
    /// Uses a short SHA256 prefix so distinct types never share a hint (unlike <see cref="object.GetHashCode"/>).
    /// </summary>
    public static string SanitizeForFileName(string metadataIdentity)
    {
        byte[] hash;
        using (var sha = SHA256.Create())
            hash = sha.ComputeHash(Encoding.UTF8.GetBytes(metadataIdentity));

        var hex = new StringBuilder(12);
        for (var i = 0; i < 6; i++)
            hex.Append(hash[i].ToString("X2"));

        var sb = new StringBuilder(metadataIdentity.Length + 16);
        foreach (var c in metadataIdentity)
        {
            if (c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
                sb.Append('_');
            else
                sb.Append(c);
        }
        sb.Append('_');
        sb.Append(hex);
        return sb.ToString();
    }
}

/// <summary>Emits <c>namespace</c> (optional) and nested <c>partial class</c> shells for the declaring type.</summary>
internal static class NestedPartialClassEmitter
{
    public static void AppendNamespace(StringBuilder sb, string? namespaceName)
    {
        if (namespaceName is not null)
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }
    }

    public static void AppendClassOpenings(StringBuilder sb, ClassKey key)
    {
        var depth = key.ClassLayers.Length;
        for (var i = 0; i < depth; i++)
        {
            var layer = key.ClassLayers[i];
            var indent = new string(' ', 4 * i);
            var head = string.IsNullOrEmpty(layer.Modifiers)
                ? $"{layer.Accessibility} partial class {layer.ClassName}"
                : $"{layer.Accessibility} {layer.Modifiers} partial class {layer.ClassName}";
            sb.AppendLine($"{indent}{head}");
            sb.AppendLine($"{indent}{{");
        }
    }

    public static string MemberIndent(ClassKey key) => new string(' ', 4 * key.ClassLayers.Length);

    public static void AppendClassClosings(StringBuilder sb, ClassKey key)
    {
        var depth = key.ClassLayers.Length;
        for (var i = depth - 1; i >= 0; i--)
            sb.AppendLine($"{new string(' ', 4 * i)}}}");
    }
}
