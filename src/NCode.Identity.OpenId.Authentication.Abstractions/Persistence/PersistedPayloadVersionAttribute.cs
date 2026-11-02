// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Declares the current at-rest schema version of a persisted-grant payload type. The version is stamped into the
/// schema-versioned envelope when the payload is written, and is the target version that
/// <see cref="IPersistedPayloadUpcaster"/> steps migrate older stored documents up to when the payload is read. A
/// payload type without this attribute is treated as schema version <c>1</c>; bump the version (and register an
/// upcaster for the new step) only for a <em>non-additive</em> change — renaming, removing, or retyping a member, or
/// removing an enum value. Purely additive changes (adding an optional member) do not require a version bump.
/// </summary>
[PublicAPI]
[AttributeUsage(
    AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Interface,
    Inherited = false
)]
public sealed class PersistedPayloadVersionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersistedPayloadVersionAttribute"/> class.
    /// </summary>
    /// <param name="version">The current at-rest schema version of the payload type. Must be at least <c>1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than <c>1</c>.</exception>
    public PersistedPayloadVersionAttribute(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }

    /// <summary>
    /// Gets the current at-rest schema version of the payload type.
    /// </summary>
    public int Version { get; }
}
