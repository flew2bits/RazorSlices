using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// Parses Razor directives (@inherits, @using) from .cshtml file content.
/// </summary>
internal static class RazorDirectiveParser
{
    /// <summary>
    /// Parses the @inherits directive value from the given source text.
    /// Returns null if no @inherits directive is found.
    /// </summary>
    internal static string? ParseInheritsDirective(SourceText sourceText)
    {
        foreach (var line in sourceText.Lines)
        {
            var lineText = line.ToString().TrimStart();
            if (lineText.StartsWith("@inherits ", StringComparison.Ordinal))
            {
                var value = lineText.Substring("@inherits ".Length).Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses all @using directives from the given source text.
    /// Returns a list of (namespace, alias) tuples. alias is null for non-aliased usings.
    /// </summary>
    internal static List<UsingDirective> ParseUsingDirectives(SourceText sourceText)
    {
        var usings = new List<UsingDirective>();
        foreach (var line in sourceText.Lines)
        {
            var lineText = line.ToString().TrimStart();
            if (lineText.StartsWith("@using ", StringComparison.Ordinal))
            {
                var value = lineText.Substring("@using ".Length).Trim();
                // Remove trailing semicolons if present
                if (value.EndsWith(";", StringComparison.Ordinal))
                {
                    value = value.Substring(0, value.Length - 1).Trim();
                }

                if (value.Length == 0)
                {
                    continue;
                }

                // Check for alias: @using Alias = Namespace.Type
                var equalsIndex = value.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var alias = value.Substring(0, equalsIndex).Trim();
                    var target = value.Substring(equalsIndex + 1).Trim();
                    if (alias.Length > 0 && target.Length > 0)
                    {
                        usings.Add(new UsingDirective(target, alias));
                    }
                }
                else
                {
                    usings.Add(new UsingDirective(value, null));
                }
            }
        }

        return usings;
    }

    /// <summary>
    /// Parses the @namespace directive value from the given source text.
    /// Returns null if no @namespace directive is found.
    /// </summary>
    internal static string? ParseNamespaceDirective(SourceText sourceText)
    {
        foreach (var line in sourceText.Lines)
        {
            var lineText = line.ToString().TrimStart();
            if (lineText.StartsWith("@namespace ", StringComparison.Ordinal))
            {
                var value = lineText.Substring("@namespace ".Length).Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }

        return null;
    }

    internal static ITypeSymbol? ExtractModelType(ResolvedDirectives directives, Compilation compilation)
    {
        if (directives.InheritsDirective is null) return null;


        var type = ResolveTypeFromString(directives.InheritsDirective, directives.UsingDirectives, compilation);
        if (type is null) throw new ModelResolutionException(directives.InheritsDirective);

        var razorSliceGeneric = compilation.GetTypeByMetadataName("RazorSlices.RazorSlice`1");
        var razorSliceNonGeneric = compilation.GetTypeByMetadataName("RazorSlices.RazorSlice");

        var current = type;
        while (current is not null)
        {
            var originalDefinition = current.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(originalDefinition, razorSliceGeneric) ||
                SymbolEqualityComparer.Default.Equals(originalDefinition, razorSliceNonGeneric))
            {
                if (current is { IsGenericType: true })
                    return current.TypeArguments.FirstOrDefault();
                break;
            }

            current = current.BaseType;
        }

        return null;
    }

    private static INamedTypeSymbol? ResolveTypeFromString(string typeString, List<UsingDirective> usingDirectives,
        Compilation compilation)
    {
        var (typeName, genericArgs) = ParseGenericType(typeString);

        var baseType = TryResolveTypeName(typeName, usingDirectives, compilation, genericArgs.Count);
        if (baseType is null) throw new ModelResolutionException(typeName);

        if (genericArgs.Count == 0) return baseType;

        var resolvedArgs = new List<ITypeSymbol>();
        foreach (var argString in genericArgs)
        {
            var resolvedArg = ResolveTypeFromString(argString, usingDirectives, compilation);
            if (resolvedArg is null) throw new ModelResolutionException(argString);
            resolvedArgs.Add(resolvedArg);
        }

        return baseType.Construct(resolvedArgs.ToArray());
    }

    private static (string typeName, List<string> genericArgs) ParseGenericType(string typeString)
    {
        typeString = typeString.Trim();

        var openAngle = typeString.IndexOf('<');
        if (openAngle < 0)
            return (typeString, []);

        var typeName = typeString.Substring(0, openAngle).Trim();
        var genericPart = typeString.Substring(openAngle + 1, typeString.Length - openAngle - 2).Trim();

        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in genericPart)
        {
            switch (ch)
            {
                case '<':
                    depth++;
                    current.Append(ch);
                    break;
                case '>':
                    depth--;
                    current.Append(ch);
                    break;
                case ',' when depth == 0:
                    args.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }
        
        if (current.Length > 0)
            args.Add(current.ToString().Trim());

        return (typeName, args);
    }

    private static INamedTypeSymbol? TryResolveTypeName(string typeName, List<UsingDirective> usingDirectives,
        Compilation compilation, int genericArity = 0)
    {
        var metadataName = genericArity > 0 ? $"{typeName}`{genericArity}" : typeName;
        
        var type = compilation.GetTypeByMetadataName(metadataName);
        if (type is not null) return type;

        foreach (var usingDirective in usingDirectives.Where(u => u.Alias is null))
        {
            var qualifiedName = $"{usingDirective.NamespaceOrType}.{metadataName}";
            type = compilation.GetTypeByMetadataName(qualifiedName);
            if (type is not null) return type;
        }

        return null;
    }

    /// <summary>
    /// Extracts the model type from a base type string.
    /// For example, "RazorSlice&lt;Models.Todo&gt;" returns "Models.Todo".
    /// Returns null if the base type is not generic (no model).
    /// </summary>
    internal static string? ExtractModelType(string baseType)
    {
        // Find the first '<' and last '>' for the generic type argument
        var openAngle = baseType.IndexOf('<');
        if (openAngle < 0)
        {
            return null;
        }

        var closeAngle = baseType.LastIndexOf('>');
        if (closeAngle <= openAngle)
        {
            return null;
        }

        var modelType = baseType.Substring(openAngle + 1, closeAngle - openAngle - 1).Trim();
        return modelType.Length > 0 ? modelType : null;
    }

    /// <summary>
    /// Extracts the base type name (without generic arguments) from a base type string.
    /// For example, "RazorSlice&lt;Models.Todo&gt;" returns "RazorSlice".
    /// </summary>
    internal static string ExtractBaseTypeName(string baseType)
    {
        var openAngle = baseType.IndexOf('<');
        return openAngle >= 0 ? baseType.Substring(0, openAngle).Trim() : baseType.Trim();
    }
}

internal readonly struct UsingDirective(string namespaceOrType, string? alias) : IEquatable<UsingDirective>
{
    /// <summary>
    /// The namespace or fully qualified type (for alias usings).
    /// </summary>
    public string NamespaceOrType { get; } = namespaceOrType;

    /// <summary>
    /// The alias, or null for non-aliased usings.
    /// </summary>
    public string? Alias { get; } = alias;

    public bool Equals(UsingDirective other) =>
        string.Equals(NamespaceOrType, other.NamespaceOrType, StringComparison.Ordinal) &&
        string.Equals(Alias, other.Alias, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is UsingDirective other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (NamespaceOrType.GetHashCode() * 397) ^ (Alias?.GetHashCode() ?? 0);
        }
    }
}