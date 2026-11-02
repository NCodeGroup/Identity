// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

/// <summary>
/// A parent-walking <see cref="ISettingScopeCollection"/> for tests that need a real scope topology; by default the
/// OpenID cascade (server, tenant, client, resource server). Its failure phrase names only the scope, so a test asserts
/// how the caller reports the phrase, not the phrase the library composes.
/// </summary>
internal sealed class TestSettingScopeCollection(params SettingScopeDescriptor[] scopes)
    : ISettingScopeCollection
{
    public static TestSettingScopeCollection OpenId { get; } =
        new(
            new SettingScopeDescriptor { Name = OpenIdSettingScopes.Server },
            new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.Tenant,
                ParentName = OpenIdSettingScopes.Server,
            },
            new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.Client,
                ParentName = OpenIdSettingScopes.Tenant,
            },
            new SettingScopeDescriptor
            {
                Name = OpenIdSettingScopes.ResourceServer,
                ParentName = OpenIdSettingScopes.Tenant,
            }
        );

    private Dictionary<string, SettingScopeDescriptor> Scopes { get; } =
        scopes.ToDictionary(scope => scope.Name, StringComparer.Ordinal);

    public int Count => Scopes.Count;

    public IEnumerator<SettingScopeDescriptor> GetEnumerator() => Scopes.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGet(
        string scopeName,
        [MaybeNullWhen(false)] out SettingScopeDescriptor descriptor
    ) => Scopes.TryGetValue(scopeName, out descriptor);

    public bool AppliesWithin(SettingDescriptor descriptor, string scopeName)
    {
        if (!Scopes.ContainsKey(scopeName))
            throw new ArgumentException($"'{scopeName}' is not registered.", nameof(scopeName));

        return descriptor.AppliesTo.Any(appliesTo => IsAncestorOrSelf(scopeName, appliesTo));
    }

    public bool AppliesWithin(
        SettingDescriptor descriptor,
        string scopeName,
        [NotNullWhen(false)] out string? failure
    )
    {
        var applies = AppliesWithin(descriptor, scopeName);
        failure = applies ? null : $"does not apply to the '{scopeName}' scope";
        return applies;
    }

    private bool IsAncestorOrSelf(string ancestorName, string scopeName)
    {
        string? current = scopeName;
        while (current is not null)
        {
            if (string.Equals(current, ancestorName, StringComparison.Ordinal))
                return true;

            current = Scopes.TryGetValue(current, out var scope) ? scope.ParentName : null;
        }

        return false;
    }
}
