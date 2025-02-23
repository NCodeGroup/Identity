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
/// Provides a default implementation of the <see cref="IReadOnlyKnownSettingCollection"/> abstraction.
/// </summary>
public class ReadOnlyKnownSettingCollection(
    IReadOnlySettingCollection store
) : IReadOnlyKnownSettingCollection
{
    private IReadOnlySettingCollection Store { get; } = store;

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenEncryptionAlgValuesSupported =>
        Store.GetValue(SettingKeys.AccessTokenEncryptionAlgValuesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenEncryptionEncValuesSupported =>
        Store.GetValue(SettingKeys.AccessTokenEncryptionEncValuesSupported);

    /// <inheritdoc />
    public bool AccessTokenEncryptionRequired =>
        Store.GetValue(SettingKeys.AccessTokenEncryptionRequired);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenEncryptionZipValuesSupported =>
        Store.GetValue(SettingKeys.AccessTokenEncryptionZipValuesSupported);

    /// <inheritdoc />
    public TimeSpan AccessTokenLifetime =>
        Store.GetValue(SettingKeys.AccessTokenLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenSigningAlgValuesSupported =>
        Store.GetValue(SettingKeys.AccessTokenSigningAlgValuesSupported);

    /// <inheritdoc />
    public string AccessTokenType =>
        Store.GetValue(SettingKeys.AccessTokenType);

    /// <inheritdoc />
    public bool AllowLoopbackRedirect =>
        Store.GetValue(SettingKeys.AllowLoopbackRedirect);

    /// <inheritdoc />
    public bool AllowPlainCodeChallengeMethod =>
        Store.GetValue(SettingKeys.AllowPlainCodeChallengeMethod);

    /// <inheritdoc />
    public bool AllowUnsafeTokenResponse =>
        Store.GetValue(SettingKeys.AllowUnsafeTokenResponse);

    /// <inheritdoc />
    public string AuthorizationAuthenticateScheme =>
        Store.GetValue(SettingKeys.AuthorizationAuthenticateScheme);

    /// <inheritdoc />
    public string AuthorizationChallengeScheme =>
        Store.GetValue(SettingKeys.AuthorizationChallengeScheme);

    /// <inheritdoc />
    public TimeSpan AuthorizationCodeLifetime =>
        Store.GetValue(SettingKeys.AuthorizationCodeLifetime);

    /// <inheritdoc />
    public TimeSpan ClockSkew =>
        Store.GetValue(SettingKeys.ClockSkew);

    /// <inheritdoc />
    public TimeSpan ContinueAuthorizationLifetime =>
        Store.GetValue(SettingKeys.ContinueAuthorizationLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> GrantTypesSupported =>
        Store.GetValue(SettingKeys.GrantTypesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionAlgValuesSupported =>
        Store.GetValue(SettingKeys.IdTokenEncryptionAlgValuesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionEncValuesSupported =>
        Store.GetValue(SettingKeys.IdTokenEncryptionEncValuesSupported);

    /// <inheritdoc />
    public bool IdTokenEncryptionRequired =>
        Store.GetValue(SettingKeys.IdTokenEncryptionRequired);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionZipValuesSupported =>
        Store.GetValue(SettingKeys.IdTokenEncryptionZipValuesSupported);

    /// <inheritdoc />
    public TimeSpan IdTokenLifetime =>
        Store.GetValue(SettingKeys.IdTokenLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported =>
        Store.GetValue(SettingKeys.IdTokenSigningAlgValuesSupported);

    /// <inheritdoc />
    public string RefreshTokenExpirationPolicy =>
        Store.GetValue(SettingKeys.RefreshTokenExpirationPolicy);

    /// <inheritdoc />
    public TimeSpan RefreshTokenLifetime =>
        Store.GetValue(SettingKeys.RefreshTokenLifetime);

    /// <inheritdoc />
    public bool RefreshTokenRotationEnabled =>
        Store.GetValue(SettingKeys.RefreshTokenRotationEnabled);

    /// <inheritdoc />
    public string RequestObjectExpectedAudience =>
        Store.GetValue(SettingKeys.RequestObjectExpectedAudience);

    /// <inheritdoc />
    public bool RequestParameterSupported =>
        Store.GetValue(SettingKeys.RequestParameterSupported);

    /// <inheritdoc />
    public bool RequestUriParameterSupported =>
        Store.GetValue(SettingKeys.RequestUriParameterSupported);

    /// <inheritdoc />
    public bool RequestUriRequireStrictContentType =>
        Store.GetValue(SettingKeys.RequestUriRequireStrictContentType);

    /// <inheritdoc />
    public string RequestUriExpectedContentType =>
        Store.GetValue(SettingKeys.RequestUriExpectedContentType);

    /// <inheritdoc />
    public bool RequireCodeChallenge =>
        Store.GetValue(SettingKeys.RequireCodeChallenge);

    /// <inheritdoc />
    public bool RequireRequestObject =>
        Store.GetValue(SettingKeys.RequireRequestObject);

    /// <inheritdoc />
    public IReadOnlyCollection<string> ResponseModesSupported =>
        Store.GetValue(SettingKeys.ResponseModesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> ScopesSupported =>
        Store.GetValue(SettingKeys.ScopesSupported);

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
    public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
        new KnownSettingCollection(Store.Merge(otherCollection));
}
