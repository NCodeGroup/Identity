// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the descriptors of the schemes a test registers, as a package contributes the descriptors of its own.
/// </summary>
internal sealed class TestSecretVerifierSchemeDataSource(IEnumerable<string> names)
    : ICollectionDataSource<SecretVerifierSchemeDescriptor>
{
    public IEnumerable<SecretVerifierSchemeDescriptor> Collection { get; } =
    [.. names.Select(name => new SecretVerifierSchemeDescriptor { Name = name })];

    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
