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
        get => Store.GetValue(SettingKeys.AccessTokenEncryptionAlgValuesSupported);
        set => Store.Set(SettingKeys.AccessTokenEncryptionAlgValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionEncValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenEncryptionEncValuesSupported
    {
        get => Store.GetValue(SettingKeys.AccessTokenEncryptionEncValuesSupported);
        set => Store.Set(SettingKeys.AccessTokenEncryptionEncValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionRequired" />
    public bool AccessTokenEncryptionRequired
    {
        get => Store.GetValue(SettingKeys.AccessTokenEncryptionRequired);
        set => Store.Set(SettingKeys.AccessTokenEncryptionRequired, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenEncryptionZipValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenEncryptionZipValuesSupported
    {
        get => Store.GetValue(SettingKeys.AccessTokenEncryptionZipValuesSupported);
        set => Store.Set(SettingKeys.AccessTokenEncryptionZipValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenLifetime" />
    public TimeSpan AccessTokenLifetime
    {
        get => Store.GetValue(SettingKeys.AccessTokenLifetime);
        set => Store.Set(SettingKeys.AccessTokenLifetime, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenSigningAlgValuesSupported" />
    public IReadOnlyCollection<string> AccessTokenSigningAlgValuesSupported
    {
        get => Store.GetValue(SettingKeys.AccessTokenSigningAlgValuesSupported);
        set => Store.Set(SettingKeys.AccessTokenSigningAlgValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AccessTokenType" />
    public string AccessTokenType
    {
        get => Store.GetValue(SettingKeys.AccessTokenType);
        set => Store.Set(SettingKeys.AccessTokenType, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowLoopbackRedirect" />
    public bool AllowLoopbackRedirect
    {
        get => Store.GetValue(SettingKeys.AllowLoopbackRedirect);
        set => Store.Set(SettingKeys.AllowLoopbackRedirect, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowPlainCodeChallengeMethod" />
    public bool AllowPlainCodeChallengeMethod
    {
        get => Store.GetValue(SettingKeys.AllowPlainCodeChallengeMethod);
        set => Store.Set(SettingKeys.AllowPlainCodeChallengeMethod, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AllowUnsafeTokenResponse" />
    public bool AllowUnsafeTokenResponse
    {
        get => Store.GetValue(SettingKeys.AllowUnsafeTokenResponse);
        set => Store.Set(SettingKeys.AllowUnsafeTokenResponse, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AuthorizationCodeLifetime" />
    public TimeSpan AuthorizationCodeLifetime
    {
        get => Store.GetValue(SettingKeys.AuthorizationCodeLifetime);
        set => Store.Set(SettingKeys.AuthorizationCodeLifetime, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.AuthorizationSignInScheme" />
    public string AuthorizationSignInScheme
    {
        get => Store.GetValue(SettingKeys.AuthorizationSignInScheme);
        set => Store.Set(SettingKeys.AuthorizationSignInScheme, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.ClockSkew" />
    public TimeSpan ClockSkew
    {
        get => Store.GetValue(SettingKeys.ClockSkew);
        set => Store.Set(SettingKeys.ClockSkew, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.ContinueAuthorizationLifetime" />
    public TimeSpan ContinueAuthorizationLifetime
    {
        get => Store.GetValue(SettingKeys.ContinueAuthorizationLifetime);
        set => Store.Set(SettingKeys.ContinueAuthorizationLifetime, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.GrantTypesSupported" />
    public IReadOnlyCollection<string> GrantTypesSupported
    {
        get => Store.GetValue(SettingKeys.GrantTypesSupported);
        set => Store.Set(SettingKeys.GrantTypesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionAlgValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionAlgValuesSupported
    {
        get => Store.GetValue(SettingKeys.IdTokenEncryptionAlgValuesSupported);
        set => Store.Set(SettingKeys.IdTokenEncryptionAlgValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionEncValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionEncValuesSupported
    {
        get => Store.GetValue(SettingKeys.IdTokenEncryptionEncValuesSupported);
        set => Store.Set(SettingKeys.IdTokenEncryptionEncValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionRequired" />
    public bool IdTokenEncryptionRequired
    {
        get => Store.GetValue(SettingKeys.IdTokenEncryptionRequired);
        set => Store.Set(SettingKeys.IdTokenEncryptionRequired, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenEncryptionZipValuesSupported" />
    public IReadOnlyCollection<string> IdTokenEncryptionZipValuesSupported
    {
        get => Store.GetValue(SettingKeys.IdTokenEncryptionZipValuesSupported);
        set => Store.Set(SettingKeys.IdTokenEncryptionZipValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenLifetime" />
    public TimeSpan IdTokenLifetime
    {
        get => Store.GetValue(SettingKeys.IdTokenLifetime);
        set => Store.Set(SettingKeys.IdTokenLifetime, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.IdTokenSigningAlgValuesSupported" />
    public IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported
    {
        get => Store.GetValue(SettingKeys.IdTokenSigningAlgValuesSupported);
        set => Store.Set(SettingKeys.IdTokenSigningAlgValuesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenExpirationPolicy" />
    public string RefreshTokenExpirationPolicy
    {
        get => Store.GetValue(SettingKeys.RefreshTokenExpirationPolicy);
        set => Store.Set(SettingKeys.RefreshTokenExpirationPolicy, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenLifetime" />
    public TimeSpan RefreshTokenLifetime
    {
        get => Store.GetValue(SettingKeys.RefreshTokenLifetime);
        set => Store.Set(SettingKeys.RefreshTokenLifetime, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RefreshTokenRotationEnabled" />
    public bool RefreshTokenRotationEnabled
    {
        get => Store.GetValue(SettingKeys.RefreshTokenRotationEnabled);
        set => Store.Set(SettingKeys.RefreshTokenRotationEnabled, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestObjectExpectedAudience" />
    public string RequestObjectExpectedAudience
    {
        get => Store.GetValue(SettingKeys.RequestObjectExpectedAudience);
        set => Store.Set(SettingKeys.RequestObjectExpectedAudience, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestParameterSupported" />
    public bool RequestParameterSupported
    {
        get => Store.GetValue(SettingKeys.RequestParameterSupported);
        set => Store.Set(SettingKeys.RequestParameterSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriParameterSupported" />
    public bool RequestUriParameterSupported
    {
        get => Store.GetValue(SettingKeys.RequestUriParameterSupported);
        set => Store.Set(SettingKeys.RequestUriParameterSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriRequireStrictContentType" />
    public bool RequestUriRequireStrictContentType
    {
        get => Store.GetValue(SettingKeys.RequestUriRequireStrictContentType);
        set => Store.Set(SettingKeys.RequestUriRequireStrictContentType, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequestUriExpectedContentType" />
    public string RequestUriExpectedContentType
    {
        get => Store.GetValue(SettingKeys.RequestUriExpectedContentType);
        set => Store.Set(SettingKeys.RequestUriExpectedContentType, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.RequireCodeChallenge" />
    public bool RequireCodeChallenge
    {
        get => Store.GetValue(SettingKeys.RequireCodeChallenge);
        set => Store.Set(SettingKeys.RequireCodeChallenge, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.ResponseModesSupported" />
    public IReadOnlyCollection<string> ResponseModesSupported
    {
        get => Store.GetValue(SettingKeys.ResponseModesSupported);
        set => Store.Set(SettingKeys.ResponseModesSupported, value);
    }

    /// <inheritdoc cref="IKnownSettingCollection.ScopesSupported" />
    public IReadOnlyCollection<string> ScopesSupported
    {
        get => Store.GetValue(SettingKeys.ScopesSupported);
        set => Store.Set(SettingKeys.ScopesSupported, value);
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

    /// <inheritdoc />
    public TValue GetValue<TValue>(SettingKey<TValue> key)
        where TValue : notnull =>
        Store.GetValue(key);

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
    public void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull =>
        Store.Set(key, value);

    /// <inheritdoc />
    public bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull =>
        Store.Remove(key);

    /// <inheritdoc />
    public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
        new KnownSettingCollection(Store.Merge(otherCollection));
}
