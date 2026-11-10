// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace NCode.Identity.Analyzers;

/// <summary>
/// The task types and the cancellation token type of a compilation, and what they say about a method's shape.
/// </summary>
// See ADR-1495.
internal sealed class AsyncTypes
{
    private INamedTypeSymbol Task { get; }
    private INamedTypeSymbol TaskOfT { get; }
    private INamedTypeSymbol ValueTask { get; }
    private INamedTypeSymbol ValueTaskOfT { get; }
    private INamedTypeSymbol CancellationToken { get; }

    private AsyncTypes(
        INamedTypeSymbol task,
        INamedTypeSymbol taskOfT,
        INamedTypeSymbol valueTask,
        INamedTypeSymbol valueTaskOfT,
        INamedTypeSymbol cancellationToken
    )
    {
        Task = task;
        TaskOfT = taskOfT;
        ValueTask = valueTask;
        ValueTaskOfT = valueTaskOfT;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Resolves the types from a compilation.
    /// </summary>
    /// <param name="compilation">The compilation to resolve the types in.</param>
    /// <returns>The types, or <see langword="null"/> when the compilation does not reference them.</returns>
    public static AsyncTypes? TryCreate(Compilation compilation)
    {
        var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        var cancellationToken = compilation.GetTypeByMetadataName(
            "System.Threading.CancellationToken"
        );

        return
            task is null
            || taskOfT is null
            || valueTask is null
            || valueTaskOfT is null
            || cancellationToken is null
            ? null
            : new AsyncTypes(task, taskOfT, valueTask, valueTaskOfT, cancellationToken);
    }

    /// <summary>
    /// Describes the parts of a method's shape that the family's rule for async methods covers.
    /// </summary>
    /// <param name="method">The method to describe.</param>
    /// <returns>The method's shape.</returns>
    public MethodShape Describe(IMethodSymbol method)
    {
        var returnType = method.ReturnType.OriginalDefinition;
        var returnsTask =
            SymbolEqualityComparer.Default.Equals(returnType, Task)
            || SymbolEqualityComparer.Default.Equals(returnType, TaskOfT);
        var returnsValueTask =
            SymbolEqualityComparer.Default.Equals(returnType, ValueTask)
            || SymbolEqualityComparer.Default.Equals(returnType, ValueTaskOfT);

        var parameters = method.Parameters;
        var takesTrailingToken =
            parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(
                parameters[parameters.Length - 1].Type,
                CancellationToken
            );

        return new MethodShape(
            returnsTask,
            (returnsTask || returnsValueTask || method.IsAsync) && !takesTrailingToken
        );
    }
}
