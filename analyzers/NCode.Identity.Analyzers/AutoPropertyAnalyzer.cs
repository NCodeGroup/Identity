// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Reports a field that could be an auto-property.
/// </summary>
// See ADR-1486.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AutoPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The identifier of the diagnostic that reports a field.
    /// </summary>
    public const string DiagnosticId = "NCID0001";

    private static DiagnosticDescriptor Rule { get; } =
        new(
            DiagnosticId,
            title: "Declare state as an auto-property",
            messageFormat: "Field '{0}' is not used by reference or as a layout target; declare it as an auto-property",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A field is allowed only where a property cannot be: a const, a ref field, a volatile or fixed-size buffer field, a field of a [StructLayout] or [InlineArray] struct, or a field passed as ref, out or in, taken with ref or &, or mutated in place as a struct.",
            helpLinkUri: "https://github.com/NCodeGroup/identity/blob/main/docs/decisions/1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md",
            customTags: WellKnownDiagnosticTags.CompilationEnd
        );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
        );
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var byReference = new ConcurrentDictionary<IFieldSymbol, bool>(
            SymbolEqualityComparer.Default
        );
        var candidates = new ConcurrentDictionary<IFieldSymbol, bool>(
            SymbolEqualityComparer.Default
        );

        context.RegisterOperationAction(
            operationContext =>
            {
                var reference = (IFieldReferenceOperation)operationContext.Operation;
                if (FieldReferences.IsByReference(reference))
                {
                    byReference.TryAdd(reference.Field.OriginalDefinition, true);
                }
            },
            OperationKind.FieldReference
        );

        context.RegisterSymbolAction(
            symbolContext =>
            {
                var field = (IFieldSymbol)symbolContext.Symbol;
                if (
                    (!symbolContext.IsGeneratedCode || IsMappedToHandWrittenSource(field))
                    && !FieldExemptions.IsExempt(field)
                )
                {
                    candidates.TryAdd(field, true);
                }
            },
            SymbolKind.Field
        );

        context.RegisterCompilationEndAction(endContext =>
        {
            foreach (var field in candidates.Keys)
            {
                if (byReference.ContainsKey(field))
                {
                    continue;
                }

                endContext.ReportDiagnostic(
                    Diagnostic.Create(Rule, field.Locations[0], field.Name)
                );
            }
        });
    }

    private static bool IsMappedToHandWrittenSource(IFieldSymbol field)
    {
        var location = field.Locations.FirstOrDefault();
        if (location is not { IsInSource: true, SourceTree: { } tree })
        {
            return false;
        }

        return location.GetMappedLineSpan().HasMappedPath
            && tree.GetLineVisibility(location.SourceSpan.Start) == LineVisibility.Visible;
    }
}
