// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Contains the outcome of comparing a presented <c>client_secret</c> with a client's stored secrets: whether one
/// matched, none matched, or a costly verification was refused before it compared, and the identifier of the credential
/// that matched.
/// </summary>
/// <param name="Result">The outcome of the comparison.</param>
/// <param name="CredentialId">The identifier of the credential that matched, which may itself be
/// <see langword="null"/>; <see langword="null"/> unless <paramref name="Result"/> is
/// <see cref="SecretVerificationResult.Success"/>.</param>
// See ADR-1391.
internal readonly record struct ClientSecretMatch(
    SecretVerificationResult Result,
    string? CredentialId
)
{
    /// <summary>
    /// Gets the outcome when no stored secret matched.
    /// </summary>
    public static ClientSecretMatch Failed { get; } = new(SecretVerificationResult.Failed, null);

    /// <summary>
    /// Gets the outcome when a costly verification was refused before it compared.
    /// </summary>
    public static ClientSecretMatch Unavailable { get; } =
        new(SecretVerificationResult.Unavailable, null);

    /// <summary>
    /// Creates the outcome when the specified credential matched.
    /// </summary>
    /// <param name="credentialId">The identifier of the credential that matched.</param>
    /// <returns>The <see cref="ClientSecretMatch"/>.</returns>
    public static ClientSecretMatch Matched(string? credentialId) =>
        new(SecretVerificationResult.Success, credentialId);
}
