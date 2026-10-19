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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides the registered scopes of the settings cascade and decides, from the topology they form, whether a setting
/// applies within a scope.
/// </summary>
/// <remarks>
/// A setting applies within a scope when it applies to the scope itself or to a scope the scope's settings cascade into
/// — a descendant of the scope. A settings document at a scope may carry exactly the settings that apply within it: a
/// setting anywhere else would be accepted without being honored.
/// </remarks>
// See ADR-0149.
[PublicAPI]
public interface ISettingScopeCollection : IReadOnlyCollection<SettingScopeDescriptor>
{
    /// <summary>
    /// Attempts to get the registered scope with the specified name.
    /// </summary>
    /// <param name="scopeName">The name of the scope to get.</param>
    /// <param name="descriptor">When this method returns, contains the registered scope, if one is found.</param>
    /// <returns><see langword="true"/> when a scope with the name is registered; otherwise, <see langword="false"/>.</returns>
    bool TryGet(string scopeName, [MaybeNullWhen(false)] out SettingScopeDescriptor descriptor);

    /// <summary>
    /// Determines whether a setting applies within a scope: it applies to the scope itself or to a scope the scope's
    /// settings cascade into.
    /// </summary>
    /// <param name="descriptor">The descriptor of the setting.</param>
    /// <param name="scopeName">The name of a registered scope.</param>
    /// <returns><see langword="true"/> when the setting applies within the scope; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="scopeName"/> is not a registered scope.</exception>
    bool AppliesWithin(SettingDescriptor descriptor, string scopeName);

    /// <summary>
    /// Determines whether a setting applies within a scope, as <see cref="AppliesWithin(SettingDescriptor, string)"/>
    /// does, and when it does not, states the scopes it applies to.
    /// </summary>
    /// <param name="descriptor">The descriptor of the setting.</param>
    /// <param name="scopeName">The name of a registered scope.</param>
    /// <param name="failure">When this method returns <see langword="false"/>, a phrase that states the scopes the
    /// setting applies to, written to follow the name of the setting; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the setting applies within the scope; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="scopeName"/> is not a registered scope.</exception>
    bool AppliesWithin(
        SettingDescriptor descriptor,
        string scopeName,
        [NotNullWhen(false)] out string? failure
    );
}
