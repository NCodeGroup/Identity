// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the request that checks the setup token before the wizard asks for anything else.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CheckSetupTokenRequest
{
    /// <summary>
    /// Gets the setup token to check.
    /// </summary>
    public required string Token { get; init; }
}
