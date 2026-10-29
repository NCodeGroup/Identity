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

using System.Collections.Frozen;
using System.Collections.Immutable;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="IRedirectUriKindCatalog"/> over the registered
/// <see cref="IRedirectUriKind"/> strategy collection. Kinds are keyed by their <see cref="IRedirectUriKind.KindName"/>.
/// </summary>
/// <remarks>
/// <para>
/// A redirect URI is permitted when it is of a registered kind that <c>redirect_uri_kinds_supported</c> lists, or of any
/// registered kind while the setting is unset. A value that is not an absolute URI, or a URI of no registered kind, is
/// never permitted.
/// </para>
/// <para>
/// A listed kind name that matches no registered kind matches no redirect URI, so a stale or misspelled name can only
/// narrow what the setting permits.
/// </para>
/// <para>
/// The constructor throws an <see cref="InvalidOperationException"/> when the registered kinds are inconsistent: a kind
/// has an empty name, a name that contains whitespace, or the same name as another kind.
/// </para>
/// </remarks>
// See ADR-0203 (redirect URI kinds).
internal sealed class DefaultRedirectUriKindCatalog : IRedirectUriKindCatalog
{
    private ImmutableArray<IRedirectUriKind> Kinds { get; }

    private FrozenDictionary<string, IRedirectUriKind> KindsByName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRedirectUriKindCatalog"/> class.
    /// </summary>
    /// <param name="kinds">The registered redirect URI kinds.</param>
    public DefaultRedirectUriKindCatalog(IEnumerable<IRedirectUriKind> kinds)
    {
        var kindsByName = new Dictionary<string, IRedirectUriKind>(StringComparer.Ordinal);
        var registeredKinds = ImmutableArray.CreateBuilder<IRedirectUriKind>();

        foreach (var kind in kinds)
        {
            var kindName = kind.KindName;

            if (string.IsNullOrEmpty(kindName) || kindName.Any(char.IsWhiteSpace))
                throw new InvalidOperationException(
                    $"The '{kind.GetType().Name}' redirect URI kind must have a name that is not empty and contains no whitespace."
                );

            if (!kindsByName.TryAdd(kindName, kind))
                throw new InvalidOperationException(
                    $"More than one redirect URI kind is named '{kindName}'."
                );

            registeredKinds.Add(kind);
        }

        Kinds = registeredKinds.ToImmutable();
        KindsByName = kindsByName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool IsPermitted(IReadOnlySettingCollection settings, string redirectUri)
    {
        if (!AbsoluteUri.TryParse(redirectUri, out var uri))
            return false;

        if (!settings.TryGetValue(OpenIdSettingKeys.RedirectUriKindsSupported, out var kindNames))
        {
            foreach (var kind in Kinds)
            {
                if (kind.Matches(uri))
                    return true;
            }

            return false;
        }

        foreach (var kindName in kindNames)
        {
            if (KindsByName.TryGetValue(kindName, out var kind) && kind.Matches(uri))
                return true;
        }

        return false;
    }
}
