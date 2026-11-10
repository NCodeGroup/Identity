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

    public static async ValueTask<ImmutableArray<Diagnostic>> AnalyzeAsync(
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
            "Snippet",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        Assert.DoesNotContain(
            compilation.GetDiagnostics(cancellationToken),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        return await compilation
            .WithAnalyzers([new AutoPropertyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
    }
}
