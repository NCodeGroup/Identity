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
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Registration;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Binds the <see cref="SecretProtectionOptions.DefaultSectionName"/> configuration section and protects the Data
/// Protection key ring under the master key of the server.
/// </summary>
// See ADR-0235.
internal static class SecretProtectionRegistration
{
    private const string RootKeyProvidersSubsection = "RootKeyProviders";

    // Every direct child of the secret protection section. The children of RootKeyProviders belong to the root key
    // providers, each of which binds its own, so strict binding does not close the set of providers.
    private static FrozenSet<string> SecretProtectionChildren { get; } =
        FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            nameof(SecretProtectionOptions.RootKeyProvider),
            nameof(SecretProtectionOptions.Recovery),
            RootKeyProvidersSubsection
        );

    /// <summary>
    /// Binds the options that configure how stored secrets are protected from <paramref name="configuration"/> and
    /// encrypts the Data Protection key ring under the master key of the server. The server's protector is loaded by a
    /// hosted service that is placed before every other one, and the key ring gate is placed right after it, so the
    /// protector is ready and every live key of the ring is known to decrypt before Data Protection loads the key ring.
    /// </summary>
    /// <remarks>
    /// Binding is strict and checked when the host starts: a key that maps to no option stops the host with an error
    /// naming the key. The <see cref="DataProtectionKeyRingOptions.DefaultSectionName"/> section revokes Data
    /// Protection keys by ID.
    /// </remarks>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> whose services are configured.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> that holds the
    /// <see cref="SecretProtectionOptions.DefaultSectionName"/> section.</param>
    /// <exception cref="InvalidOperationException">The section has a direct child other than the root key provider,
    /// the recovery options and the root key provider options.</exception>
    public static void AddSecretProtection(
        IIdentityServerBuilder builder,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(SecretProtectionOptions.DefaultSectionName);
        ThrowOnUnknownChild(section);

        var recoverySection = section.GetSection(nameof(SecretProtectionOptions.Recovery));
        var localSection = configuration.GetSection(LocalKeyWrapperOptions.DefaultSectionName);
        var keyRingSection = configuration.GetSection(
            DataProtectionKeyRingOptions.DefaultSectionName
        );

        var serviceCollection = builder.ServiceCollection;

        // ValidateOnStart materializes each options instance as the host starts, so a strict-binding failure stops the
        // host before it accepts traffic rather than on the first request that reads the option (ADR-0137).
        serviceCollection
            .AddOptions<SecretProtectionOptions>()
            .Configure(options =>
            {
                options.RootKeyProvider = section[nameof(SecretProtectionOptions.RootKeyProvider)];
                recoverySection.BindStrictly(options.Recovery);
            })
            .ValidateOnStart();

        serviceCollection
            .AddOptions<LocalKeyWrapperOptions>()
            .Configure(options => localSection.BindStrictly(options))
            .ValidateOnStart();

        serviceCollection
            .AddOptions<DataProtectionKeyRingOptions>()
            .Configure(options => keyRingSection.BindStrictly(options))
            .ValidateOnStart();

        serviceCollection.TryAddSingleton<ServerSecretProtectorHolder>();
        serviceCollection.TryAddSingleton(TimeProvider.System);

        // The gate resolves IKeyManager, which Data Protection registers.
        serviceCollection.AddDataProtection();

        // Hosted services start in registration order, and Data Protection registers one that loads the key ring, so
        // the warmup goes first to have the protector ready before the key ring is read. The key ring gate goes right
        // after it: it needs the protector the warmup loads to decrypt the keys, and it must refuse the host before
        // Data Protection's own hosted service reads the ring. A startup validator cannot do this, because validators
        // run before every hosted service, so before the warmup.
        if (
            !serviceCollection.Any(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ServerSecretProtectorWarmup)
            )
        )
        {
            serviceCollection.Insert(
                0,
                ServiceDescriptor.Singleton<IHostedService, ServerSecretProtectorWarmup>()
            );
        }

        if (
            !serviceCollection.Any(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(DataProtectionKeyRingGate)
            )
        )
        {
            serviceCollection.Insert(
                1,
                ServiceDescriptor.Singleton<IHostedService, DataProtectionKeyRingGate>()
            );
        }

        serviceCollection
            .AddOptions<KeyManagementOptions>()
            .Configure<ServerSecretProtectorHolder>(
                (options, holder) => options.XmlEncryptor = new MasterKeyXmlEncryptor(holder)
            );
    }

    private static void ThrowOnUnknownChild(IConfigurationSection section)
    {
        var unknownPaths = section
            .GetChildren()
            .Where(child => !SecretProtectionChildren.Contains(child.Key))
            .Select(child => $"'{child.Path}'")
            .ToList();

        if (unknownPaths.Count == 0)
            return;

        throw new InvalidOperationException(
            $"The configuration section '{section.Path}' contains unrecognized key(s): "
                + $"{string.Join(", ", unknownPaths)}. Its recognized keys are "
                + $"{string.Join(", ", SecretProtectionChildren.Order(StringComparer.Ordinal))}."
        );
    }
}
