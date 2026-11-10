// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NCode.Identity.Analyzers;

/// <summary>
/// Reports an own method that returns <c>Task</c> or <c>Task&lt;T&gt;</c>, and an own asynchronous method whose last
/// parameter is not a <c>CancellationToken</c>, unless a framework contract fixes the method's shape.
/// </summary>
// See ADR-1495.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncMethodShapeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The identifier of the diagnostic that reports a method returning <c>Task</c> or <c>Task&lt;T&gt;</c>.
    /// </summary>
    public const string TaskReturnDiagnosticId = "NCID0002";

    /// <summary>
    /// The identifier of the diagnostic that reports an asynchronous method without a trailing
    /// <c>CancellationToken</c>.
    /// </summary>
    public const string CancellationTokenDiagnosticId = "NCID0003";

    private const string HelpLinkUri =
        "https://github.com/NCodeGroup/Identity/blob/main/docs/decisions/1495-an-analyzer-reports-an-own-async-method-that-returns-task-or-takes-no-trailing-cancellation-token.md";

    private static DiagnosticDescriptor TaskReturnRule { get; } =
        new(
            TaskReturnDiagnosticId,
            title: "Return ValueTask from an own async method",
            messageFormat: "Method '{0}' returns '{1}'; return ValueTask or ValueTask<T> unless a framework contract requires a Task",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "An own method returns ValueTask or ValueTask<T>. Task is allowed only where a framework contract fixes it: an override or implementation of a member an assembly outside the family declares, an xUnit test method, an ASP.NET Core middleware Invoke or InvokeAsync method, an entry point, the dispose pattern's DisposeAsyncCore, or a method an ASP.NET Core API takes as a delegate.",
            helpLinkUri: HelpLinkUri,
            customTags: WellKnownDiagnosticTags.CompilationEnd
        );

    private static DiagnosticDescriptor CancellationTokenRule { get; } =
        new(
            CancellationTokenDiagnosticId,
            title: "Take a CancellationToken last in an own async method",
            messageFormat: "Method '{0}' is asynchronous and does not take a CancellationToken as its last parameter",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "An own asynchronous method takes a CancellationToken as its last parameter, even when none of its awaited calls takes one. A framework contract that fixes the method's shape is exempt.",
            helpLinkUri: HelpLinkUri,
            customTags: WellKnownDiagnosticTags.CompilationEnd
        );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [TaskReturnRule, CancellationTokenRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var types = AsyncTypes.TryCreate(context.Compilation);
        if (types is null)
        {
            return;
        }

        var assemblyName = context.Compilation.AssemblyName;
        var frameworkBound = new ConcurrentDictionary<IMethodSymbol, bool>(
            SymbolEqualityComparer.Default
        );
        var candidates = new ConcurrentDictionary<IMethodSymbol, MethodShape>(
            SymbolEqualityComparer.Default
        );

        context.RegisterOperationAction(
            operationContext =>
            {
                var reference = (IMethodReferenceOperation)operationContext.Operation;
                if (MethodExemptions.IsPassedToAspNetCore(reference))
                {
                    frameworkBound.TryAdd(reference.Method.OriginalDefinition, true);
                }
            },
            OperationKind.MethodReference
        );

        context.RegisterSymbolAction(
            symbolContext =>
            {
                var method = (IMethodSymbol)symbolContext.Symbol;
                if (
                    method.MethodKind != MethodKind.Ordinary
                    || method.IsImplicitlyDeclared
                    || MethodExemptions.IsFrameworkContract(method, assemblyName)
                )
                {
                    return;
                }

                var shape = types.Describe(method);
                if (shape.ReturnsTask || shape.LacksTrailingToken)
                {
                    candidates.TryAdd(method, shape);
                }
            },
            SymbolKind.Method
        );

        context.RegisterCompilationEndAction(endContext =>
        {
            foreach (var candidate in candidates)
            {
                var method = candidate.Key;
                if (frameworkBound.ContainsKey(method.OriginalDefinition))
                {
                    continue;
                }

                if (candidate.Value.ReturnsTask)
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(
                            TaskReturnRule,
                            method.Locations[0],
                            method.Name,
                            method.ReturnType.ToDisplayString(
                                SymbolDisplayFormat.MinimallyQualifiedFormat
                            )
                        )
                    );
                }

                if (candidate.Value.LacksTrailingToken)
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(CancellationTokenRule, method.Locations[0], method.Name)
                    );
                }
            }
        });
    }
}
