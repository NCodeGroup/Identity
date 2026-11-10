// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Analyzers;

/// <summary>
/// The parts of a method's shape that the family's rule for async methods covers.
/// </summary>
/// <param name="returnsTask">Whether the method returns <c>Task</c> or <c>Task&lt;T&gt;</c>.</param>
/// <param name="lacksTrailingToken">
/// Whether the method is asynchronous and does not take a <c>CancellationToken</c> as its last parameter.
/// </param>
// See ADR-1496.
internal readonly struct MethodShape(bool returnsTask, bool lacksTrailingToken)
{
    /// <summary>
    /// Gets a value that indicates whether the method returns <c>Task</c> or <c>Task&lt;T&gt;</c>.
    /// </summary>
    public bool ReturnsTask { get; } = returnsTask;

    /// <summary>
    /// Gets a value that indicates whether the method is asynchronous and does not take a <c>CancellationToken</c> as
    /// its last parameter.
    /// </summary>
    public bool LacksTrailingToken { get; } = lacksTrailingToken;
}
