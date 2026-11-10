// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace NCode.Identity.Analyzers;

internal static class AnalyzerHarness
{
    private static ImmutableArray<MetadataReference> References { get; } = LoadReferences();

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var paths = (trustedAssemblies ?? string.Empty).Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );
        return [.. paths.Select(path => MetadataReference.CreateFromFile(path))];
    }

    /// <summary>
    /// Compiles a source text into an in-memory assembly with a given name, so a snippet can reference a type that an
    /// assembly outside the snippet declares.
    /// </summary>
    public static MetadataReference CreateReference(
        string assemblyName,
        string source,
        CancellationToken cancellationToken
    )
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            cancellationToken: cancellationToken
        );
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: cancellationToken);
        var message = string.Join(Environment.NewLine, result.Diagnostics);
        Assert.True(result.Success, message);

        var image = stream.ToArray();
        return MetadataReference.CreateFromImage(image);
    }

    public static ValueTask<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CancellationToken cancellationToken
    ) => AnalyzeAsync(source, analyzer, [], cancellationToken);

    public static async ValueTask<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        ImmutableArray<MetadataReference> additionalReferences,
        CancellationToken cancellationToken
    )
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            cancellationToken: cancellationToken
        );
        var references = References.AddRange(additionalReferences);
        var compilation = CSharpCompilation.Create(
            "Snippet",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        Assert.DoesNotContain(
            compilation.GetDiagnostics(cancellationToken),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        return await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
    }
}
