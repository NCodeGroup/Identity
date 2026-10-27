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
using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Describes how one JWK key type (<c>kty</c>) is thumbprinted (RFC 7638 §3.2) and which of its members hold private
/// key material. A contributed key type, one that a package outside this library adds, registers a descriptor so
/// that its keys can be thumbprinted and screened for private material like the built-in key types.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7638#section-3.2">RFC 7638 Section 3.2 - JWK Members Used in the Thumbprint Computation</seealso>
[PublicAPI]
public sealed class JsonWebKeyTypeDescriptor
{
    /// <summary>
    /// Gets the value of the <c>kty</c> member that this descriptor applies to.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the required members of the key type, including <c>kty</c>, that make up the thumbprint input
    /// (RFC 7638 §3.2), distinct and in ordinal lexicographic order.
    /// </summary>
    public ImmutableArray<string> ThumbprintMembers { get; }

    /// <summary>
    /// Gets the members of the key type that hold private key material.
    /// </summary>
    public FrozenSet<string> PrivateMembers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebKeyTypeDescriptor"/> class.
    /// </summary>
    /// <param name="name">The value of the <c>kty</c> member that this descriptor applies to.</param>
    /// <param name="thumbprintMembers">
    /// The required public members of the key type that make up the thumbprint input. <c>kty</c> is always included
    /// and need not be listed.
    /// </param>
    /// <param name="privateMembers">The members of the key type that hold private key material.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, a member name is null or empty, or <paramref name="privateMembers"/>
    /// contains <c>kty</c>.
    /// </exception>
    public JsonWebKeyTypeDescriptor(
        string name,
        IEnumerable<string> thumbprintMembers,
        IEnumerable<string> privateMembers
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(thumbprintMembers);
        ArgumentNullException.ThrowIfNull(privateMembers);

        var thumbprintList = thumbprintMembers.ToList();
        var privateList = privateMembers.ToList();

        foreach (var member in thumbprintList.Concat(privateList))
        {
            ArgumentException.ThrowIfNullOrEmpty(member, nameof(thumbprintMembers));
        }

        if (privateList.Contains(JsonWebKeyParameterNames.KeyType, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"The '{JsonWebKeyParameterNames.KeyType}' member cannot be a private member.",
                nameof(privateMembers)
            );
        }

        Name = name;
        ThumbprintMembers =
        [
            .. thumbprintList
                .Append(JsonWebKeyParameterNames.KeyType)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        PrivateMembers = privateList.ToFrozenSet(StringComparer.Ordinal);
    }
}
