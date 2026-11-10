// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

/// <summary>
/// Provides the optional inputs of a validation request that a test sends to the logout token validator.
/// </summary>
internal sealed class LogoutTokenValidateOptions
{
    /// <summary>
    /// Gets the client registration to validate against, or <see langword="null"/> for the registration of the tests.
    /// </summary>
    public ClientRegistration? Registration { get; init; }
}
