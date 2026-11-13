// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the setup page's view of an uninitialized instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetupStatusResponse
{
    /// <summary>
    /// Gets the source of the setup token, one of the <see cref="SetupTokenSources"/> values.
    /// </summary>
    public required string TokenSource { get; init; }

    /// <summary>
    /// Gets the minimum length of the root administrator's password.
    /// </summary>
    public required int MinimumPasswordLength { get; init; }
}
