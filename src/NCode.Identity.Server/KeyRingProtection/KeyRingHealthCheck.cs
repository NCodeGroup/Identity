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

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NCode.Identity.Server.KeyRingProtection;

/// <summary>
/// Reports whether this replica can unwrap every live key that was already in the Data Protection key ring when the
/// replica started checking its readiness. A key is live until it expires or is revoked. A replica that cannot unwrap
/// such a key, for example because the certificate that wrapped it was renewed in place (ADR-0177), cannot read the data
/// that key protected, and Data Protection quietly creates a new default key in its place. The check keeps that replica
/// out of service and stops a rollout before it replaces the healthy replicas. A key created after the replica started
/// is not checked: when a misconfigured replica writes one, the replicas that were serving stay ready.
/// </summary>
/// <remarks>
/// The check is registered as a singleton, so the start of its readiness checks is the time it was first resolved.
/// </remarks>
internal sealed class KeyRingHealthCheck : IHealthCheck
{
    /// <summary>
    /// The name of the health check.
    /// </summary>
    internal const string Name = "data-protection-key-ring";

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyRingHealthCheck"/> class.
    /// </summary>
    /// <param name="keyManager">The <see cref="IKeyManager"/> that reads the key ring.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> that supplies the current time.</param>
    public KeyRingHealthCheck(IKeyManager keyManager, TimeProvider timeProvider)
    {
        KeyManager = keyManager;
        TimeProvider = timeProvider;
        StartedAt = timeProvider.GetUtcNow();
    }

    private IKeyManager KeyManager { get; }

    private TimeProvider TimeProvider { get; }

    private DateTimeOffset StartedAt { get; }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var now = TimeProvider.GetUtcNow();
        var unreadableKeyIds = KeyManager
            .GetAllKeys()
            .Where(key =>
                !key.IsRevoked
                && key.CreationDate <= StartedAt
                && key.ExpirationDate > now
                && !CanUnwrap(key)
            )
            .Select(key => key.KeyId)
            .ToList();

        if (unreadableKeyIds.Count == 0)
            return Task.FromResult(HealthCheckResult.Healthy());

        var description =
            "This replica cannot unwrap the live Data Protection keys "
            + string.Join(", ", unreadableKeyIds)
            + ". Configure the certificate or key that wrapped them, for example as a previous certificate in "
            + "'DataProtection:KeyProtection', or revoke the keys.";
        return Task.FromResult(
            new HealthCheckResult(context.Registration.FailureStatus, description)
        );
    }

    private static bool CanUnwrap(IKey key)
    {
        try
        {
            return key.CreateEncryptor() is not null;
        }
        // Data Protection's own key resolver treats any failure to create a key's encryptor as an unusable key, because
        // unwrapping runs the configured decryptor, which may be a third-party key management service.
        catch (Exception)
        {
            return false;
        }
    }
}
