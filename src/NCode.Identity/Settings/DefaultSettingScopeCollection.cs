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

using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingScopeCollection"/> abstraction. The registered scopes
/// must form a forest — unique names, each parent registered, no cycles — so the scopes a scope's settings cascade into
/// are computed once, when the collection is built.
/// </summary>
// See ADR-0149.
internal sealed class DefaultSettingScopeCollection : ISettingScopeCollection
{
    private FrozenDictionary<string, SettingScopeDescriptor> ScopesByName { get; }

    // The scope itself and every scope below it: the scopes whose effective settings a document at the scope reaches.
    private FrozenDictionary<string, FrozenSet<string>> CascadeByName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSettingScopeCollection"/> class.
    /// </summary>
    /// <param name="scopes">The registered scopes.</param>
    /// <exception cref="InvalidOperationException">A scope name is registered more than once, a parent is not
    /// registered, or the parents form a cycle.</exception>
    public DefaultSettingScopeCollection(IEnumerable<SettingScopeDescriptor> scopes)
    {
        var scopesByName = new Dictionary<string, SettingScopeDescriptor>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            if (!scopesByName.TryAdd(scope.Name, scope))
            {
                throw new InvalidOperationException(
                    $"The setting scope '{scope.Name}' is registered more than once."
                );
            }
        }

        var cascades = scopesByName.Keys.ToDictionary(
            name => name,
            name => new HashSet<string>(StringComparer.Ordinal) { name },
            StringComparer.Ordinal
        );

        foreach (var scope in scopesByName.Values)
        {
            // Walk up from the scope, adding it to the cascade of each ancestor; a revisited scope is a cycle.
            var visited = new HashSet<string>(StringComparer.Ordinal) { scope.Name };
            var parentName = scope.ParentName;
            while (parentName is not null)
            {
                if (!scopesByName.TryGetValue(parentName, out var parent))
                {
                    throw new InvalidOperationException(
                        $"The setting scope '{scope.Name}' names the parent scope '{parentName}', which is not registered."
                    );
                }

                if (!visited.Add(parentName))
                {
                    throw new InvalidOperationException(
                        $"The setting scope '{scope.Name}' is its own ancestor; the parents of the setting scopes must not form a cycle."
                    );
                }

                cascades[parentName].Add(scope.Name);
                parentName = parent.ParentName;
            }
        }

        ScopesByName = scopesByName.ToFrozenDictionary(StringComparer.Ordinal);
        CascadeByName = cascades.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenSet(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public int Count => ScopesByName.Count;

    /// <inheritdoc />
    public IEnumerator<SettingScopeDescriptor> GetEnumerator() =>
        ((IEnumerable<SettingScopeDescriptor>)ScopesByName.Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool TryGet(
        string scopeName,
        [MaybeNullWhen(false)] out SettingScopeDescriptor descriptor
    ) => ScopesByName.TryGetValue(scopeName, out descriptor);

    /// <inheritdoc />
    public bool AppliesWithin(SettingDescriptor descriptor, string scopeName)
    {
        var cascade = GetCascade(scopeName);
        return !descriptor.AppliesTo.IsDefault && descriptor.AppliesTo.Any(cascade.Contains);
    }

    /// <inheritdoc />
    public bool AppliesWithin(
        SettingDescriptor descriptor,
        string scopeName,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (AppliesWithin(descriptor, scopeName))
        {
            failure = null;
            return true;
        }

        var appliesTo = descriptor.AppliesTo.IsDefault
            ? []
            : descriptor.AppliesTo.Where(ScopesByName.ContainsKey).Distinct().ToList();

        failure =
            appliesTo.Count == 0
                ? "applies to no registered scope, so no settings document may carry it"
                : $"does not apply to the '{scopeName}' scope; it applies to the {DescribeScopes(appliesTo)}";
        return false;
    }

    private FrozenSet<string> GetCascade(string scopeName) =>
        CascadeByName.TryGetValue(scopeName, out var cascade)
            ? cascade
            : throw new ArgumentException(
                $"'{scopeName}' is not a registered setting scope.",
                nameof(scopeName)
            );

    private static string DescribeScopes(List<string> scopeNames)
    {
        switch (scopeNames.Count)
        {
            case 1:
                return $"'{scopeNames[0]}' scope";
            case 2:
                return $"'{scopeNames[0]}' and '{scopeNames[1]}' scopes";
        }

        var leading = scopeNames[..^1].Select(name => $"'{name}'");
        return $"{string.Join(", ", leading)}, and '{scopeNames[^1]}' scopes";
    }
}
