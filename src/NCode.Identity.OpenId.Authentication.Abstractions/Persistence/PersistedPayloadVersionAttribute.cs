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
