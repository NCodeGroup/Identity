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

using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Represents a settings document that a store holds and that names a setting, a value or a scope the registered
/// settings cannot honor. The exception names the document, so it can be found and replaced.
/// </summary>
/// <param name="scopeName">The registered scope name of the document.</param>
/// <param name="ownerId">The identifier of the server, tenant, client or resource server that owns the
/// document.</param>
/// <param name="innerException">The error that the settings vocabulary reported for the document.</param>
// See ADR-0245.
[PublicAPI]
public sealed class UnreadableSettingsException(
    string scopeName,
    string ownerId,
    JsonException innerException
) : JsonException(CreateMessage(scopeName, ownerId, innerException), innerException)
{
    private static string CreateMessage(
        string scopeName,
        string ownerId,
        JsonException innerException
    )
    {
        return $"The stored settings document of {scopeName} '{ownerId}' cannot be honored: {innerException.Message}";
    }

    // The property initializers run before the base constructor's arguments are evaluated, so the arguments are
    // guarded here.
    /// <summary>
    /// Gets the registered scope name of the document.
    /// </summary>
    public string ScopeName { get; } =
        scopeName ?? throw new ArgumentNullException(nameof(scopeName));

    /// <summary>
    /// Gets the identifier of the server, tenant, client or resource server that owns the document.
    /// </summary>
    public string OwnerId { get; } = ownerId ?? throw new ArgumentNullException(nameof(ownerId));

    /// <summary>
    /// Gets the message of the error that the settings vocabulary reported for the document.
    /// </summary>
    public string DocumentError { get; } =
        innerException?.Message ?? throw new ArgumentNullException(nameof(innerException));
}
