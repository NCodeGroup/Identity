// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the response to a setup token that is the current one.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CheckSetupTokenResponse
{
    /// <summary>
    /// Gets the management console that the deployment names for the wizard's console step, or <c>null</c> when the
    /// deployment names none.
    /// </summary>
    public SetupManagementConsoleHint? ManagementConsole { get; init; }
}
