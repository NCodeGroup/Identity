#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the PKCE (RFC 7636) setting whose domain is the
/// registered <see cref="ICodeChallengeMethod"/> transformations, composed with the base descriptor catalog from the
/// core slice. A value that names no registered transformation is rejected rather than stored and never matched. The
/// default is <c>S256</c> alone: registering another transformation adds it to the domain, and an operator enables it
/// by listing it, so a transformation is never offered only because it is registered.
/// </summary>
// See ADR-0144 and ADR-0140.
internal class DefaultPkceSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    ICodeChallengeMethodCollectionProvider codeChallengeMethodCollectionProvider
) : ICollectionDataSource<SettingDescriptor>
{
    private static readonly string[] DefaultMethods = [OpenIdConstants.CodeChallengeMethods.S256];

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private ImmutableArray<string> MethodNames { get; } =
    [.. codeChallengeMethodCollectionProvider.SupportedMethods.Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // code_challenge_methods_supported: the PKCE code_challenge_method transformations the server supports
            // (RFC 7636 / RFC 8414). Only S256 ships and is the default; 'plain' exposes the verifier in the
            // authorization request and is not a transformation the server offers (OAuth 2.1 §7.5.2).
            // Ceiling (Intersect): a child narrows the parent's set; it cannot re-add a method the parent removed.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.CodeChallengeMethodsSupported,
                Rule = SettingRules.Each(SettingRules.OneOf(MethodNames)),
                Default = [.. DefaultMethods.Intersect(MethodNames, StringComparer.Ordinal)],

                IsDiscoverable = true,
                OnMerge = SettingMerge.Intersect,
            };
        }
    }
}
