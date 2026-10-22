#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> for a collection of <see cref="Setting"/> instances
/// that are loaded from the server's configured settings (<see cref="OpenIdServerOptions.Settings"/>) and the default
/// values from <see cref="ISettingDescriptorCollectionProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The configured settings are read through <see cref="ISettingConfigurationReader"/>, so a setting that cannot be
/// honored is never loaded. The host validates them when it starts; when a later configuration reload cannot be
/// honored, the reload is rejected with an error log and the previously loaded settings remain in force.
/// </para>
/// <para>
/// The server is the top of the settings cascade, so the security profiles its settings select are expanded through
/// <see cref="ISecurityProfileCatalog"/> exactly as a tenant's or a client's are, and they then bind every tenant and
/// client below it. Its descriptor defaults, computed supported values and default-settings-provider values are a
/// baseline beneath its configuration, so a selected profile's default replaces them but never a configured value.
/// </para>
/// </remarks>
// See ADR-0148 (security profiles accumulate down the cascade).
internal class RootSettingsCollectionDataSource : IDisposableCollectionDataSource<Setting>
{
    private IConfigurationSection? ConfigurationSection { get; }
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; }
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; }
    private ISettingCollectionFactory SettingCollectionFactory { get; }
    private IEnumerable<IDefaultSettingsProvider> DefaultSettingsProviders { get; }
    private ISettingConfigurationReader SettingConfigurationReader { get; }
    private ISecurityProfileCatalog SecurityProfileCatalog { get; }
    private ILogger<RootSettingsCollectionDataSource> Logger { get; }

    private Lock SyncObj { get; } = new();
    private bool IsDisposed { get; set; }

    private List<IDisposable>? ChangeTokenRegistrations { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }

    /// <inheritdoc />
    public IEnumerable<Setting> Collection { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RootSettingsCollectionDataSource"/> class.
    /// </summary>
    public RootSettingsCollectionDataSource(
        IOptions<OpenIdOptions> optionsAccessor,
        IAlgorithmCollectionProvider algorithmCollectionProvider,
        ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
        ISettingCollectionFactory settingCollectionFactory,
        IEnumerable<IDefaultSettingsProvider> defaultSettingsProviders,
        ISettingConfigurationReader settingConfigurationReader,
        ISecurityProfileCatalog securityProfileCatalog,
        ILogger<RootSettingsCollectionDataSource> logger
    )
    {
        ConfigurationSection = optionsAccessor.Value.Server.Settings;
        AlgorithmCollectionProvider = algorithmCollectionProvider;
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
        SettingCollectionFactory = settingCollectionFactory;
        DefaultSettingsProviders = defaultSettingsProviders;
        SettingConfigurationReader = settingConfigurationReader;
        SecurityProfileCatalog = securityProfileCatalog;
        Logger = logger;

        Collection = LoadCollection();

        List<IDisposable> changeTokenRegistrations =
        [
            ChangeToken.OnChange(AlgorithmCollectionProvider.GetChangeToken, HandleChange),
            ChangeToken.OnChange(SettingDescriptorCollectionProvider.GetChangeToken, HandleChange),
        ];

        if (ConfigurationSection is { } configurationSection)
        {
            changeTokenRegistrations.Add(
                ChangeToken.OnChange(configurationSection.GetReloadToken, HandleChange)
            );
        }

        ChangeTokenRegistrations = changeTokenRegistrations;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed || !disposing)
            return;

        List<IDisposable>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            disposables = [];

            if (ChangeTokenRegistrations is { Count: > 0 })
            {
                disposables.AddRange(ChangeTokenRegistrations);
            }

            if (ChangeTokenSource is not null)
            {
                disposables.Add(ChangeTokenSource);
            }

            Collection = [];
            ChangeTokenRegistrations = null;

            ChangeTokenSource = null;
            ConsumerChangeToken = null;
        }

        disposables.DisposeAll();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ConsumerChangeToken is not null)
            return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null)
                return;

            ThrowIfDisposed();
            RefreshConsumerChangeToken();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void RefreshConsumerChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }

    private void HandleChange()
    {
        CancellationTokenSource? oldTokenSource;

        if (IsDisposed)
            return;
        lock (SyncObj)
        {
            if (IsDisposed)
                return;

            // A reload that cannot be honored leaves the last valid settings in force rather than loading part of it
            // or faulting the reload callback; the error log is the signal (ADR-0139).
            if (!CanLoadConfiguration())
                return;

            oldTokenSource = ChangeTokenSource;

            ChangeTokenSource = null;
            ConsumerChangeToken = null;

            // refresh the cached change token
            if (oldTokenSource is not null)
            {
                RefreshConsumerChangeToken();
            }

            // refresh the cached collection
            Collection = LoadCollection();
        }

        // this will trigger the consumer change token
        // and most importantly after the collection has been refreshed
        oldTokenSource?.Cancel();

        oldTokenSource?.Dispose();
    }

    private IEnumerable<Setting> LoadCollection()
    {
        // The descriptor defaults, the computed supported values and the default-settings providers are the baseline
        // beneath the server's configuration, so a selected security profile's default replaces them but never a
        // configured value (ADR-0148).
        var baselineSettings = SettingCollectionFactory.Create();
        LoadDefaults(baselineSettings);
        LoadSupportedAlgorithms(baselineSettings);
        LoadDefaultSettingsProviders(baselineSettings);

        var configuredSettings = SettingCollectionFactory.Create();
        LoadFromConfiguration(configuredSettings);

        return SecurityProfileCatalog.Expand(
            configuredSettings,
            OpenIdSettingScopes.Server,
            baselineSettings
        );
    }

    private void LoadDefaultSettingsProviders(ISettingCollection settings)
    {
        foreach (var provider in DefaultSettingsProviders)
        {
            provider.Configure(settings);
        }
    }

    private void LoadDefaults(ISettingCollection settings)
    {
        foreach (var descriptor in SettingDescriptorCollectionProvider.Collection)
        {
            if (descriptor.HasDefault)
            {
                settings.Set(descriptor.CreateDefault());
            }
        }
    }

    private void LoadSupportedAlgorithms(ISettingCollection settings)
    {
        var signingAlgValuesSupported = new List<string>();
        var asymmetricSigningAlgValuesSupported = new List<string>();
        var encryptionAlgValuesSupported = new List<string>();
        var asymmetricEncryptionAlgValuesSupported = new List<string>();
        var encryptionEncValuesSupported = new List<string>();

        // Compression algorithms are never offered: the server compresses nothing it encrypts and accepts no compressed
        // JWT (RFC 8725 §3.6), so no setting names one.
        foreach (var algorithm in AlgorithmCollectionProvider.Collection)
        {
            if (!SettingAlgorithms.IsSelectable(algorithm))
                continue;

            switch (algorithm.Type)
            {
                case AlgorithmType.DigitalSignature:
                    signingAlgValuesSupported.Add(algorithm.Code);
                    // Secure-by-default: tokens that cross a trust boundary (id_token, userinfo, access_token)
                    // advertise only asymmetric signing algorithms — never symmetric HS* (RFC 9700 posture).
                    if (SettingAlgorithms.IsAsymmetricSignature(algorithm))
                    {
                        asymmetricSigningAlgValuesSupported.Add(algorithm.Code);
                    }
                    break;

                case AlgorithmType.KeyManagement:
                    encryptionAlgValuesSupported.Add(algorithm.Code);
                    // A response addressed to a client is encrypted to the client's public key, and a request object
                    // addressed to the server to the server's, so only asymmetric key management applies to them.
                    if (SettingAlgorithms.IsAsymmetricKeyManagement(algorithm))
                    {
                        asymmetricEncryptionAlgValuesSupported.Add(algorithm.Code);
                    }
                    break;

                case AlgorithmType.AuthenticatedEncryption:
                    encryptionEncValuesSupported.Add(algorithm.Code);
                    break;
            }
        }

        // Cross-trust-boundary token signing advertises asymmetric algorithms only (no HS*); configuration may narrow
        // these lists but never name a symmetric algorithm, because the settings' domain excludes them (ADR-0140).
        settings.Set(
            OpenIdSettingKeys.IdTokenSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.UserInfoSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        // A JWT introspection response is addressed to a resource server across a trust boundary (RFC 9701), and a JWT-secured
        // authorization response to a client (JARM).
        settings.Set(
            OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AuthorizationSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        // DPoP proofs must be signed with an asymmetric algorithm (RFC 9449); never symmetric HS*.
        settings.Set(
            OpenIdSettingKeys.DPoPSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        // A signed CIBA authentication request must use an asymmetric signature (CIBA Core §7.1.1).
        settings.Set(
            OpenIdSettingKeys.BackchannelAuthenticationRequestSigningAlgValuesSupported,
            asymmetricSigningAlgValuesSupported
        );
        // A request object may legitimately be signed with the client secret (OpenID Connect Core §10.1), so it keeps
        // the full set.
        settings.Set(
            OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported,
            signingAlgValuesSupported
        );

        // ID tokens, UserInfo responses, JWT introspection responses, and JWT-secured authorization responses are encrypted to the client's public key, and
        // request objects to the server's, so they advertise asymmetric key management only (ADR-0155). An access token
        // may be encrypted to a resource server that holds a shared key, so it keeps the full set.
        settings.Set(
            OpenIdSettingKeys.IdTokenEncryptionAlgValuesSupported,
            asymmetricEncryptionAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported,
            asymmetricEncryptionAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.IntrospectionEncryptionAlgValuesSupported,
            asymmetricEncryptionAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AuthorizationEncryptionAlgValuesSupported,
            asymmetricEncryptionAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported,
            encryptionAlgValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.RequestObjectEncryptionAlgValuesSupported,
            asymmetricEncryptionAlgValuesSupported
        );

        settings.Set(
            OpenIdSettingKeys.IdTokenEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.IntrospectionEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AuthorizationEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );
        settings.Set(
            OpenIdSettingKeys.RequestObjectEncryptionEncValuesSupported,
            encryptionEncValuesSupported
        );

        settings.Set(
            OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported,
            signingAlgValuesSupported
        );
    }

    private bool CanLoadConfiguration()
    {
        if (ConfigurationSection is not { } configurationSection)
            return true;

        var errors = SettingConfigurationReader.Validate(
            configurationSection,
            OpenIdSettingScopes.Server
        );
        if (errors.Count == 0)
            return true;

        var details = string.Join(" ", errors);
        Logger.ServerSettingsReloadRejected(configurationSection.Path, details);
        return false;
    }

    private void LoadFromConfiguration(ISettingCollection settings)
    {
        if (ConfigurationSection is null)
            return;

        foreach (
            var setting in SettingConfigurationReader.Read(
                ConfigurationSection,
                OpenIdSettingScopes.Server
            )
        )
        {
            settings.Set(setting);
        }
    }
}
