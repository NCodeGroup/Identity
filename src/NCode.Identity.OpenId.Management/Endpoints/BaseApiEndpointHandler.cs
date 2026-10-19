#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Extends <see cref="ManagementApiEndpointHandler"/> with the management implementation's own secret- and
/// settings-projection helpers shared by the client, tenant, and server endpoint handlers.
/// </summary>
internal abstract class BaseApiEndpointHandler : ManagementApiEndpointHandler
{
    /// <summary>
    /// Serializes the specified <see cref="JsonObject"/> into a detached <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to serialize.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    internal virtual JsonElement SerializeToElement(JsonObject jsonObject)
    {
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    /// <summary>
    /// Converts the specified <see cref="JsonElement"/> into a mutable <see cref="JsonObject"/>, returning an empty
    /// object when the element is <c>null</c> or not a JSON object.
    /// </summary>
    /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
    /// <returns>The resulting <see cref="JsonObject"/>.</returns>
    internal virtual JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? [];

            default:
                return [];
        }
    }

    /// <summary>
    /// Serializes an effective (merged) <see cref="IReadOnlySettingCollection"/> into a flat <see cref="JsonElement"/>
    /// of setting name to formatted value, ordered by name for a stable dump and using the same value representation
    /// as the discovery document. Only the settings that apply within the scope are included, so the preview shows what
    /// governs that scope and omits what the runtime never reads there — a tenant-wide session setting in a client's
    /// preview, or a client setting in a resource server's.
    /// </summary>
    /// <param name="settings">The effective <see cref="IReadOnlySettingCollection"/> to serialize.</param>
    /// <param name="scopes">The registered <see cref="ISettingScopeCollection"/> that decides where a setting applies.</param>
    /// <param name="scopeName">The name of the scope whose effective settings <paramref name="settings"/> are.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    // See ADR-0149.
    internal virtual JsonElement ToEffectiveSettingsElement(
        IReadOnlySettingCollection settings,
        ISettingScopeCollection scopes,
        string scopeName
    )
    {
        var values = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            var descriptor = setting.Descriptor;
            if (scopes.AppliesWithin(descriptor, scopeName))
            {
                values[descriptor.Name] = descriptor.Format(setting);
            }
        }

        return JsonSerializer.SerializeToElement(values);
    }

    /// <summary>
    /// Builds the <c>400 Bad Request</c> validation problem for a settings document that cannot be honored: the
    /// problem's <c>errors</c> map each offending setting name to its error, and its <c>detail</c> lists them all.
    /// </summary>
    /// <param name="errors">The <see cref="SettingError"/> instances describing each setting that cannot be honored.</param>
    /// <returns>The <see cref="IResult"/> representing the validation failure.</returns>
    internal virtual IResult SettingsValidationProblem(IReadOnlyList<SettingError> errors)
    {
        var messages = errors.Select(error => error.Message);
        var detail = string.Join(" ", messages);
        var errorsBySetting = errors
            .GroupBy(error => error.SettingName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal
            );

        return TypedResults.ValidationProblem(
            errorsBySetting,
            detail: detail,
            title: "The settings are invalid."
        );
    }

    /// <summary>
    /// Maps a collection of <see cref="PersistedSecret"/> instances to their <see cref="SecretResource"/> representations.
    /// </summary>
    /// <param name="secrets">The collection of <see cref="PersistedSecret"/> instances to map.</param>
    /// <returns>The mapped read-only collection of <see cref="SecretResource"/> instances.</returns>
    internal virtual IReadOnlyCollection<SecretResource> ToSecretsResource(
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        return secrets.Select(ToSecretResource).ToList();
    }

    /// <summary>
    /// Maps a single <see cref="PersistedSecret"/> instance to its <see cref="SecretResource"/> representation.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> instance to map.</param>
    /// <returns>The mapped <see cref="SecretResource"/>.</returns>
    internal virtual SecretResource ToSecretResource(PersistedSecret secret)
    {
        return new SecretResource
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
        };
    }

    /// <summary>
    /// Maps a single <see cref="PersistedSecret"/> instance to its <see cref="CreatedSecretResource"/>
    /// representation, carrying the one-time unprotected key material to reveal. Used only by the
    /// client-secret create path; every read path returns the material-free <see cref="SecretResource"/>.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> instance to map.</param>
    /// <param name="secretMaterial">The one-time unprotected material to reveal, or <c>null</c> when none.</param>
    /// <returns>The mapped <see cref="CreatedSecretResource"/>.</returns>
    internal virtual CreatedSecretResource ToCreatedSecretResource(
        PersistedSecret secret,
        string? secretMaterial
    )
    {
        return new CreatedSecretResource
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            SecretMaterial = secretMaterial,
        };
    }
}
