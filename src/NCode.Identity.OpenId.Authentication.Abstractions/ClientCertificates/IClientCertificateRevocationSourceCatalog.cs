// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Resolves the registered sources of revocation information by name.
/// </summary>
/// <remarks>
/// The registered sources are the <see cref="ClientCertificateRevocationSourceDescriptor"/> instances that packages
/// contribute. The catalog is independent of the <see cref="IClientCertificateRevocationSource"/> implementations, so
/// reading it constructs no source. The validator of the revocation options reads it as the host starts, so an
/// implementation depends on nothing that reads those options and resolves no source: one that did would build its
/// graph inside options validation and deadlock the host's composition.
/// </remarks>
[PublicAPI]
public interface IClientCertificateRevocationSourceCatalog
{
    /// <summary>
    /// Gets the names of the registered sources in the default order: by
    /// <see cref="ClientCertificateRevocationSourceDescriptor.Order"/>, then by name with the ordinal comparison.
    /// </summary>
    IReadOnlyList<string> SourceNames { get; }

    /// <summary>
    /// Attempts to get the descriptor of the registered source with the specified name.
    /// </summary>
    /// <param name="sourceName">The name of the source.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the descriptor of the source; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a source with the specified name is registered; otherwise, <c>false</c>.</returns>
    bool TryGet(
        string sourceName,
        [NotNullWhen(true)] out ClientCertificateRevocationSourceDescriptor? descriptor
    );
}
