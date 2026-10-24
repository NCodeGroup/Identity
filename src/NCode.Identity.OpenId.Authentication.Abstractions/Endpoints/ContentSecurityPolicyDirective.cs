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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents one directive of a page's Content-Security-Policy: a name and the sources it allows. A directive with
/// no sources allows nothing.
/// </summary>
/// <param name="name">
/// The directive name, which consists of lowercase ASCII letters, digits and hyphens.
/// </param>
/// <param name="sources">
/// The sources the directive allows, each of which is a run of printable ASCII characters without <c>;</c> and
/// <c>,</c>, so that no source can start another directive.
/// </param>
[PublicAPI]
public sealed class ContentSecurityPolicyDirective(string name, params IEnumerable<string> sources)
{
    /// <summary>
    /// Gets the directive name.
    /// </summary>
    public string Name { get; } = ValidateName(name);

    /// <summary>
    /// Gets the sources the directive allows, in the order they are written.
    /// </summary>
    public ImmutableArray<string> Sources { get; } = ValidateSources(sources);

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var character in name)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                throw new ArgumentException(
                    "A directive name consists of lowercase ASCII letters, digits and hyphens.",
                    nameof(name)
                );
            }
        }

        return name;
    }

    private static ImmutableArray<string> ValidateSources(IEnumerable<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        ImmutableArray<string> buffer = [.. sources];
        foreach (var source in buffer)
        {
            if (!IsValidSource(source))
            {
                throw new ArgumentException(
                    "A source is a non-empty run of printable ASCII characters without ';' and ','.",
                    nameof(sources)
                );
            }
        }

        return buffer;
    }

    private static bool IsValidSource(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        foreach (var character in source)
        {
            if (character is < '!' or > '~' or ';' or ',')
            {
                return false;
            }
        }

        return true;
    }
}
