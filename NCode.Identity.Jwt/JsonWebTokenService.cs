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

using System.Runtime.ExceptionServices;
using System.Text.Json;
using NCode.Jose;
using NCode.Jose.SecretKeys;

namespace NCode.Identity.Jwt;

/// <summary>
/// Provides the ability to create and validate a Json Web Token (JWT).
/// See the following RFCs for more information:
/// https://datatracker.ietf.org/doc/html/rfc7519
/// https://datatracker.ietf.org/doc/html/rfc7515
/// </summary>
public interface IJsonWebTokenService
{
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
public class JsonWebTokenService : IJsonWebTokenService
{
    private IJoseSerializer JoseSerializer { get; }
    private ISecretKeyProvider SecretKeyProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebTokenService"/> class.
    /// </summary>
    /// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
    /// <param name="secretKeyProvider">An <see cref="ISecretKeyProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
    public JsonWebTokenService(IJoseSerializer joseSerializer, ISecretKeyProvider secretKeyProvider)
    {
        JoseSerializer = joseSerializer;
        SecretKeyProvider = secretKeyProvider;
    }

    /// <inheritdoc />
    public async ValueTask<ValidateJwtResult> ValidateJwtAsync(
        string token,
        ValidateJwtParameters parameters,
        CancellationToken cancellationToken)
    {
        var propertyBag = parameters.PropertyBag.Clone();
        try
        {
            var compactJwt = JoseSerializer.ParseCompactJwt(token);

            var validationKeys = await parameters.ResolveValidationKeysAsync(
                compactJwt,
                propertyBag,
                SecretKeyProvider,
                parameters.SecretKeyTags,
                cancellationToken);

            var payload = DeserializePayload(
                compactJwt,
                validationKeys,
                out var secretKey);

            var decodedJwt = new DecodedJwt(compactJwt, payload, secretKey);
            var context = new ValidateJwtContext(secretKey, decodedJwt, propertyBag);
            await InvokeValidatorsAsync(context, parameters.Validators, cancellationToken);

            return ValidateJwtResult.Success(parameters, propertyBag, decodedJwt);
        }
        catch (Exception exception)
        {
            return ValidateJwtResult.Fail(parameters, propertyBag, token, exception);
        }
    }

    private JsonElement DeserializePayload(
        CompactJwt compactJwt,
        IEnumerable<SecretKey> validationKeys,
        out SecretKey secretKey)
    {
        var exceptions = new List<Exception>();
        // TODO: not all keys will have a key id
        var keysAttempted = new HashSet<string>();
        foreach (var keyAttempt in validationKeys)
        {
            try
            {
                keysAttempted.Add(keyAttempt.KeyId);
                var payload = JoseSerializer.Deserialize<JsonElement>(compactJwt, keyAttempt);

                secretKey = keyAttempt;
                return payload;
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        // TODO: use custom exception with message about which keys were attempted

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException(exceptions);
    }

    private static async ValueTask InvokeValidatorsAsync(
        ValidateJwtContext context,
        IEnumerable<ValidateJwtAsync> validators,
        CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            await validator(context, cancellationToken);
        }
    }
}
