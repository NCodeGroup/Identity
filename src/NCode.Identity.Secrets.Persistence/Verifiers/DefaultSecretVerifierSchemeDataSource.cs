// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the <see cref="SecretVerifierSchemeDescriptor"/> of each secret verifier scheme the family registers: the
/// <see cref="SecretVerifierSchemes.Sha256"/> and <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/> schemes.
/// </summary>
// See ADR-1391.
internal sealed class DefaultSecretVerifierSchemeDataSource
    : ICollectionDataSource<SecretVerifierSchemeDescriptor>
{
    // The descriptors never change; a token over a token that cannot be cancelled never fires.
    private static IChangeToken NeverChanges { get; } =
        new CancellationChangeToken(CancellationToken.None);

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NeverChanges;

    /// <inheritdoc />
    public IEnumerable<SecretVerifierSchemeDescriptor> Collection
    {
        get
        {
            yield return new SecretVerifierSchemeDescriptor { Name = SecretVerifierSchemes.Sha256 };
            yield return new SecretVerifierSchemeDescriptor
            {
                Name = SecretVerifierSchemes.Pbkdf2Sha256,
            };
        }
    }
}
