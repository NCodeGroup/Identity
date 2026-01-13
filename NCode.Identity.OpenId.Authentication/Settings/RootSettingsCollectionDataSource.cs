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
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> for a collection of <see cref="Setting"/> instances
/// that are loaded from a <see cref="IConfigurationSection"/> and the default values from <see cref="ISettingDescriptorCollectionProvider"/>.
/// </summary>
public class RootSettingsCollectionDataSource : IDisposableCollectionDataSource<Setting>
{
    private IConfigurationSection ConfigurationSection { get; }
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; }
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; }
    private ISettingCollectionFactory SettingCollectionFactory { get; }

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
        IConfigurationSection configurationSection,
        IAlgorithmCollectionProvider algorithmCollectionProvider,
        ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
        ISettingCollectionFactory settingCollectionFactory
    )
    {
        ConfigurationSection = configurationSection;
        AlgorithmCollectionProvider = algorithmCollectionProvider;
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
        SettingCollectionFactory = settingCollectionFactory;

        Collection = LoadCollection();

        ChangeTokenRegistrations =
        [
            ChangeToken.OnChange(ConfigurationSection.GetReloadToken, HandleChange),
            ChangeToken.OnChange(AlgorithmCollectionProvider.GetChangeToken, HandleChange),
            ChangeToken.OnChange(SettingDescriptorCollectionProvider.GetChangeToken, HandleChange),
        ];
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
        if (IsDisposed || !disposing) return;

        List<IDisposable>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
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
        if (ConsumerChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null) return;

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

        if (IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;

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
        var settings = SettingCollectionFactory.Create();

        LoadDefaults(settings);
        LoadSupportedAlgorithms(settings);
        LoadFromConfiguration(settings);

        return settings.AsEnumerable();
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
        var encryptionAlgValuesSupported = new List<string>();
        var encryptionEncValuesSupported = new List<string>();
        var encryptionZipValuesSupported = new List<string>();

        foreach (var algorithm in AlgorithmCollectionProvider.Collection)
        {
            if (string.IsNullOrEmpty(algorithm.Code)) continue;
            if (algorithm.Code == "dir") continue;
            if (algorithm.Code == "none") continue;

            switch (algorithm.Type)
            {
                case AlgorithmType.DigitalSignature:
                    signingAlgValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.KeyManagement:
                    encryptionAlgValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.AuthenticatedEncryption:
                    encryptionEncValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.Compression:
                    encryptionZipValuesSupported.Add(algorithm.Code);
                    break;
            }
        }

        settings.Set(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported, signingAlgValuesSupported);

        settings.Set(OpenIdSettingKeys.IdTokenEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(OpenIdSettingKeys.RequestObjectEncryptionAlgValuesSupported, encryptionAlgValuesSupported);

        settings.Set(OpenIdSettingKeys.IdTokenEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(OpenIdSettingKeys.RequestObjectEncryptionEncValuesSupported, encryptionEncValuesSupported);

        settings.Set(OpenIdSettingKeys.IdTokenEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(OpenIdSettingKeys.UserInfoEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(OpenIdSettingKeys.AccessTokenEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(OpenIdSettingKeys.RequestObjectEncryptionZipValuesSupported, encryptionZipValuesSupported);

        settings.Set(OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported, signingAlgValuesSupported);
    }

    private void LoadFromConfiguration(ISettingCollection settings)
    {
        var descriptors = SettingDescriptorCollectionProvider.Collection;
        foreach (var settingSection in ConfigurationSection.GetChildren())
        {
            var settingName = settingSection.Key;
            if (!descriptors.TryGet(settingName, out var descriptor))
            {
                if (settingSection.GetChildren().Count() > 1)
                {
                    descriptor = new SettingDescriptor<IReadOnlyCollection<string>>
                    {
                        Name = settingName
                    };
                }
                else
                {
                    descriptor = new SettingDescriptor<string>
                    {
                        Name = settingName
                    };
                }
            }

            var value = settingSection.Get(descriptor.ValueType);
            if (value is null) continue;

            var setting = descriptor.Create(value);
            settings.Set(setting);
        }
    }
}
