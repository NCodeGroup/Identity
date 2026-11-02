// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Indicates the disposition of an operation.
/// </summary>
[PublicAPI]
public readonly record struct ReadOnlyOperationDisposition<TError>
{
    /// <summary>
    /// Gets the error that occurred during the operation.
    /// </summary>
    public TError? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    /// <summary>
    /// Gets a value indicating whether <see cref="Error"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;
}
