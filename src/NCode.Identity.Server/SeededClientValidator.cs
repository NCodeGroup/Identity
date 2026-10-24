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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.Server;

/// <summary>
/// Checks the settings of a client that a configuration seeder is about to create, so a seeder stores no client that
/// the server cannot honor or that its tenant's settings contradict.
/// </summary>
/// <remarks>
/// A seeded client's settings come from configuration and materialize only after the seed. The validator first checks
/// the document against the registered setting domains, and then resolves the server's committed settings and the
/// tenant's, so every coherence rule sees the client as the materialized client will be.
/// </remarks>
// See ADR-0140 and ADR-0206.
internal sealed class SeededClientValidator(
    ISettingSerializer settingSerializer,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator
)
{
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;

    /// <summary>
    /// Ensures that a client a seeder is about to create has settings the server can honor and that are coherent with
    /// the settings of its tenant.
    /// </summary>
    /// <param name="clientKind">The kind of client the seeder creates, which names the client in the error.</param>
    /// <param name="clientId">The identifier of the client.</param>
    /// <param name="tenant">The tenant the client is seeded into.</param>
    /// <param name="settings">The client's own settings as a JSON document.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the client's settings are acceptable.</returns>
    /// <exception cref="InvalidOperationException">The settings cannot be honored, or a client coherence rule refuses
    /// them.</exception>
    public async ValueTask EnsureCanBeSeededAsync(
        string clientKind,
        string clientId,
        PersistedTenant tenant,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        var jsonOptions = OpenIdEnvironmentProvider.Get().JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            settings,
            OpenIdSettingScopes.Client,
            jsonOptions
        );
        if (settingErrors.Count != 0)
        {
            throw CreateException(
                clientKind,
                clientId,
                settingErrors.Select(error => error.Message)
            );
        }

        var serverSettings = await EffectiveSettingsResolver.ResolveServerAsync(cancellationToken);
        var tenantSettings = EffectiveSettingsResolver.Resolve(
            serverSettings,
            OpenIdSettingScopes.Tenant,
            tenant.Settings.Value
        );

        var violations = ClientCoherenceValidator.Validate(tenantSettings, settings);
        if (violations.Count != 0)
        {
            throw CreateException(
                clientKind,
                clientId,
                violations.Select(violation => violation.Description)
            );
        }
    }

    private static InvalidOperationException CreateException(
        string clientKind,
        string clientId,
        IEnumerable<string> messages
    ) => new($"The {clientKind} '{clientId}' cannot be seeded: {string.Join(" ", messages)}");
}
