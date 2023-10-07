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

using Microsoft.Extensions.Options;
using NCode.Identity.JsonWebTokens.Options;
using NCode.Jose;
using NCode.Jose.SecretKeys;
using NCode.SystemClock;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides the ability to create and validate a Json Web Token (JWT).
/// See the following RFCs for more information:
/// https://datatracker.ietf.org/doc/html/rfc7519
/// https://datatracker.ietf.org/doc/html/rfc7515
/// </summary>
public interface IJsonWebTokenService
{
    /// <summary>
    /// Encodes a Json Web Token (JWT) using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="parameters">The <see cref="EncodeJwtParameters"/> instance that specifies how to encode the token.</param>
    /// <returns>The encoded JWT in compact serialization format.</returns>
    string EncodeJwt(EncodeJwtParameters parameters);

    /// <summary>
    /// Validates a Json Web Token (JWT) that is encoded using JWS or JWE compact serialization format.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="parameters">An <see cref="ValidateJwtParameters"/> instance that specifies how to perform the validation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// results of the validated JWT.</returns>
    ValueTask<ValidateJwtResult> ValidateJwtAsync(
        string token,
        ValidateJwtParameters parameters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJsonWebTokenService"/> interface.
/// </summary>
public partial class JsonWebTokenService : IJsonWebTokenService
{
    private JsonWebTokenServiceOptions Options { get; }
    private IServiceProvider ServiceProvider { get; }
    private ISystemClockSecondsAccuracy SystemClock { get; }
    private IJoseSerializer JoseSerializer { get; }
    private ISecretKeyProvider SecretKeyProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebTokenService"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An accessor that provides <see cref="JsonWebTokenServiceOptions"/>.</param>
    /// <param name="serviceProvider">An <see cref="IServiceProvider"/> that can be used to resolve services.</param>
    /// <param name="systemClock">An <see cref="ISystemClockSecondsAccuracy"/> that can be used to get the current time.</param>
    /// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
    /// <param name="secretKeyProvider">An <see cref="ISecretKeyProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
    public JsonWebTokenService(
        IOptions<JsonWebTokenServiceOptions> optionsAccessor,
        IServiceProvider serviceProvider,
        ISystemClockSecondsAccuracy systemClock,
        IJoseSerializer joseSerializer,
        ISecretKeyProvider secretKeyProvider)
    {
        Options = optionsAccessor.Value;
        ServiceProvider = serviceProvider;
        SystemClock = systemClock;
        JoseSerializer = joseSerializer;
        SecretKeyProvider = secretKeyProvider;
    }
}
