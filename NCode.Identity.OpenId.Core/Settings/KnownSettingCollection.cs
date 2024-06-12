#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="IKnownSettingCollection"/> abstraction.
/// </summary>
public class KnownSettingCollection(
    ISettingCollection store
) : IKnownSettingCollection, IReadOnlyKnownSettingCollection
{
    private ISettingCollection Store { get; } = store;

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionAlgValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenEncryptionAlgValuesSupported
    {
        get => Get(KnownSettings.AccessTokenEncryptionAlgValuesSupported);
        set => Store.Set(KnownSettings.AccessTokenEncryptionAlgValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionEncValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenEncryptionEncValuesSupported
    {
        get => Get(KnownSettings.AccessTokenEncryptionEncValuesSupported);
        set => Store.Set(KnownSettings.AccessTokenEncryptionEncValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionRequired" />
    public bool AccessTokenEncryptionRequired
    {
        get => Get(KnownSettings.AccessTokenEncryptionRequired);
        set => Store.Set(KnownSettings.AccessTokenEncryptionRequired.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionZipValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenEncryptionZipValuesSupported
    {
        get => Get(KnownSettings.AccessTokenEncryptionZipValuesSupported);
        set => Store.Set(KnownSettings.AccessTokenEncryptionZipValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenLifetime" />
    public TimeSpan AccessTokenLifetime
    {
        get => Get(KnownSettings.AccessTokenLifetime);
        set => Store.Set(KnownSettings.AccessTokenLifetime.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenSigningAlgValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenSigningAlgValuesSupported
    {
        get => Get(KnownSettings.AccessTokenSigningAlgValuesSupported);
        set => Store.Set(KnownSettings.AccessTokenSigningAlgValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenType" />
    public string AccessTokenType
    {
        get => Get(KnownSettings.AccessTokenType);
        set => Store.Set(KnownSettings.AccessTokenType.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowLoopbackRedirect" />
    public bool AllowLoopbackRedirect
    {
        get => Get(KnownSettings.AllowLoopbackRedirect);
        set => Store.Set(KnownSettings.AllowLoopbackRedirect.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowPlainCodeChallengeMethod" />
    public bool AllowPlainCodeChallengeMethod
    {
        get => Get(KnownSettings.AllowPlainCodeChallengeMethod);
        set => Store.Set(KnownSettings.AllowPlainCodeChallengeMethod.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowUnsafeTokenResponse" />
    public bool AllowUnsafeTokenResponse
    {
        get => Get(KnownSettings.AllowUnsafeTokenResponse);
        set => Store.Set(KnownSettings.AllowUnsafeTokenResponse.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AuthorizationCodeLifetime" />
    public TimeSpan AuthorizationCodeLifetime
    {
        get => Get(KnownSettings.AuthorizationCodeLifetime);
        set => Store.Set(KnownSettings.AuthorizationCodeLifetime.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.AuthorizationSignInScheme" />
    public string AuthorizationSignInScheme
    {
        get => Get(KnownSettings.AuthorizationSignInScheme);
        set => Store.Set(KnownSettings.AuthorizationSignInScheme.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.ClockSkew" />
    public TimeSpan ClockSkew
    {
        get => Get(KnownSettings.ClockSkew);
        set => Store.Set(KnownSettings.ClockSkew.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.ContinueAuthorizationLifetime" />
    public TimeSpan ContinueAuthorizationLifetime
    {
        get => Get(KnownSettings.ContinueAuthorizationLifetime);
        set => Store.Set(KnownSettings.ContinueAuthorizationLifetime.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.GrantTypesSupported" />
    public IReadOnlyCollection<string> GrantTypesSupported
    {
        get => Get(KnownSettings.GrantTypesSupported);
        set => Store.Set(KnownSettings.GrantTypesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionAlgValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionAlgValuesSupported
    {
        get => Get(KnownSettings.IdTokenEncryptionAlgValuesSupported);
        set => Store.Set(KnownSettings.IdTokenEncryptionAlgValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionEncValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionEncValuesSupported
    {
        get => Get(KnownSettings.IdTokenEncryptionEncValuesSupported);
        set => Store.Set(KnownSettings.IdTokenEncryptionEncValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionRequired" />
    public bool IdTokenEncryptionRequired
    {
        get => Get(KnownSettings.IdTokenEncryptionRequired);
        set => Store.Set(KnownSettings.IdTokenEncryptionRequired.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionZipValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionZipValuesSupported
    {
        get => Get(KnownSettings.IdTokenEncryptionZipValuesSupported);
        set => Store.Set(KnownSettings.IdTokenEncryptionZipValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenLifetime" />
    public TimeSpan IdTokenLifetime
    {
        get => Get(KnownSettings.IdTokenLifetime);
        set => Store.Set(KnownSettings.IdTokenLifetime.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenSigningAlgValuesSupported" />
    public IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported
    {
        get => Get(KnownSettings.IdTokenSigningAlgValuesSupported);
        set => Store.Set(KnownSettings.IdTokenSigningAlgValuesSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenExpirationPolicy" />
    public string RefreshTokenExpirationPolicy
    {
        get => Get(KnownSettings.RefreshTokenExpirationPolicy);
        set => Store.Set(KnownSettings.RefreshTokenExpirationPolicy.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenLifetime" />
    public TimeSpan RefreshTokenLifetime
    {
        get => Get(KnownSettings.RefreshTokenLifetime);
        set => Store.Set(KnownSettings.RefreshTokenLifetime.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenRotationEnabled" />
    public bool RefreshTokenRotationEnabled
    {
        get => Get(KnownSettings.RefreshTokenRotationEnabled);
        set => Store.Set(KnownSettings.RefreshTokenRotationEnabled.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestObjectExpectedAudience" />
    public string RequestObjectExpectedAudience
    {
        get => Get(KnownSettings.RequestObjectExpectedAudience);
        set => Store.Set(KnownSettings.RequestObjectExpectedAudience.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestParameterSupported" />
    public bool RequestParameterSupported
    {
        get => Get(KnownSettings.RequestParameterSupported);
        set => Store.Set(KnownSettings.RequestParameterSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriParameterSupported" />
    public bool RequestUriParameterSupported
    {
        get => Get(KnownSettings.RequestUriParameterSupported);
        set => Store.Set(KnownSettings.RequestUriParameterSupported.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriRequireStrictContentType" />
    public bool RequestUriRequireStrictContentType
    {
        get => Get(KnownSettings.RequestUriRequireStrictContentType);
        set => Store.Set(KnownSettings.RequestUriRequireStrictContentType.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriExpectedContentType" />
    public string RequestUriExpectedContentType
    {
        get => Get(KnownSettings.RequestUriExpectedContentType);
        set => Store.Set(KnownSettings.RequestUriExpectedContentType.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequireCodeChallenge" />
    public bool RequireCodeChallenge
    {
        get => Get(KnownSettings.RequireCodeChallenge);
        set => Store.Set(KnownSettings.RequireCodeChallenge.Create(value));
    }

    /// <inheritdoc cref="IKnownSettingCollection.ScopesSupported" />
    public IReadOnlyCollection<string> ScopesSupported
    {
        get => Get(KnownSettings.ScopesSupported);
        set => Store.Set(KnownSettings.ScopesSupported.Create(value));
    }

    //

    /// <inheritdoc />
    public int Count =>
        Store.Count;

    /// <inheritdoc />
    [MustDisposeResource]
    public IEnumerator<Setting> GetEnumerator() =>
        Store.GetEnumerator();

    /// <inheritdoc />
    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)Store).GetEnumerator();

    //

    private TValue Get<TValue>(SettingDescriptor<TValue> descriptor)
        where TValue : notnull =>
        Store.TryGet(descriptor.Key, out var setting) ? setting.Value : descriptor.Default;

    /// <inheritdoc />
    public bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting) =>
        Store.TryGet(settingName, out setting);

    /// <inheritdoc />
    public bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull =>
        Store.TryGet(key, out setting);

    /// <inheritdoc />
    public void Set(Setting setting) =>
        Store.Set(setting);

    /// <inheritdoc />
    public bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull =>
        Store.Remove(key);

    /// <inheritdoc />
    public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
        new KnownSettingCollection(Store.Merge(otherCollection));
}
