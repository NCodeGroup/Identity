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
        Get(KnownSettings.AccessTokenEncryptionAlgValuesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenEncryptionEncValuesSupported =>
        Get(KnownSettings.AccessTokenEncryptionEncValuesSupported);

    /// <inheritdoc />
    public bool AccessTokenEncryptionRequired =>
        Get(KnownSettings.AccessTokenEncryptionRequired);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenEncryptionZipValuesSupported =>
        Get(KnownSettings.AccessTokenEncryptionZipValuesSupported);

    /// <inheritdoc />
    public TimeSpan AccessTokenLifetime =>
        Get(KnownSettings.AccessTokenLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AccessTokenSigningAlgValuesSupported =>
        Get(KnownSettings.AccessTokenSigningAlgValuesSupported);

    /// <inheritdoc />
    public string AccessTokenType =>
        Get(KnownSettings.AccessTokenType);

    /// <inheritdoc />
    public bool AllowLoopbackRedirect =>
        Get(KnownSettings.AllowLoopbackRedirect);

    /// <inheritdoc />
    public bool AllowPlainCodeChallengeMethod =>
        Get(KnownSettings.AllowPlainCodeChallengeMethod);

    /// <inheritdoc />
    public bool AllowUnsafeTokenResponse =>
        Get(KnownSettings.AllowUnsafeTokenResponse);

    /// <inheritdoc />
    public TimeSpan AuthorizationCodeLifetime =>
        Get(KnownSettings.AuthorizationCodeLifetime);

    /// <inheritdoc />
    public string AuthorizationSignInScheme =>
        Get(KnownSettings.AuthorizationSignInScheme);

    /// <inheritdoc />
    public TimeSpan ClockSkew =>
        Get(KnownSettings.ClockSkew);

    /// <inheritdoc />
    public TimeSpan ContinueAuthorizationLifetime =>
        Get(KnownSettings.ContinueAuthorizationLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> GrantTypesSupported =>
        Get(KnownSettings.GrantTypesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionAlgValuesSupported =>
        Get(KnownSettings.IdTokenEncryptionAlgValuesSupported);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionEncValuesSupported =>
        Get(KnownSettings.IdTokenEncryptionEncValuesSupported);

    /// <inheritdoc />
    public bool IdTokenEncryptionRequired =>
        Get(KnownSettings.IdTokenEncryptionRequired);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenEncryptionZipValuesSupported =>
        Get(KnownSettings.IdTokenEncryptionZipValuesSupported);

    /// <inheritdoc />
    public TimeSpan IdTokenLifetime =>
        Get(KnownSettings.IdTokenLifetime);

    /// <inheritdoc />
    public IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported =>
        Get(KnownSettings.IdTokenSigningAlgValuesSupported);

    /// <inheritdoc />
    public string RequestObjectExpectedAudience =>
        Get(KnownSettings.RequestObjectExpectedAudience);

    /// <inheritdoc />
    public bool RequestParameterSupported =>
        Get(KnownSettings.RequestParameterSupported);

    /// <inheritdoc />
    public bool RequestUriParameterSupported =>
        Get(KnownSettings.RequestUriParameterSupported);

    /// <inheritdoc />
    public bool RequestUriRequireStrictContentType =>
        Get(KnownSettings.RequestUriRequireStrictContentType);

    /// <inheritdoc />
    public string RequestUriExpectedContentType =>
        Get(KnownSettings.RequestUriExpectedContentType);

    /// <inheritdoc />
    public bool RequireCodeChallenge =>
        Get(KnownSettings.RequireCodeChallenge);

    //

    /// <inheritdoc />
    public int Count =>
        Store.Count;

    /// <inheritdoc />
    public IEnumerator<Setting> GetEnumerator() =>
        Store.GetEnumerator();

    /// <inheritdoc />
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
    public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
        new KnownSettingCollection(Store.Merge(otherCollection));
}
