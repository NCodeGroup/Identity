// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

/// <summary>
/// Provides the optional inputs of a validation request that a test sends to the ID token validator.
/// </summary>
internal sealed class IdTokenValidateOptions
{
    /// <summary>
    /// Gets the nonce that the ID token must carry, or <see langword="null"/> for none.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets the maximum age of the authentication, or <see langword="null"/> for none.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Gets the subject that the ID token must carry, or <see langword="null"/> for none.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the access token that the <c>at_hash</c> claim must match, or <see langword="null"/> for none.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the authorization code that the <c>c_hash</c> claim must match, or <see langword="null"/> for none.
    /// </summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>
    /// Gets the state that the <c>s_hash</c> claim must match, or <see langword="null"/> for none.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the client registration to validate against, or <see langword="null"/> for the registration of the tests.
    /// </summary>
    public ClientRegistration? Registration { get; init; }
}
