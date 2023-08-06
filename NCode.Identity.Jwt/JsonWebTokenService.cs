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
using System.Security.Claims;
using NCode.Cryptography.Keys;
using NCode.Jose;

namespace NCode.Identity.Jwt;

internal class JsonWebTokenService
{
    private IJoseSerializer JoseSerializer { get; }

    public JsonWebTokenService(IJoseSerializer joseSerializer)
    {
        JoseSerializer = joseSerializer;
    }

    // https://datatracker.ietf.org/doc/html/rfc7519
    public async ValueTask<ValidateJwtResult> ValidateAsync(
        string token,
        ValidateJwtParameters parameters,
        CancellationToken cancellationToken)
    {
        var propertyBag = parameters.PropertyBag.Clone();
        try
        {
            var compactJwt = JoseSerializer.ParseCompactJwt(token);

            var secretKeys = await ResolveSecretKeysAsync(compactJwt, parameters, propertyBag, cancellationToken);
            var payload = DeserializePayload(compactJwt, secretKeys, out var secretKey);

            var decodedJwt = new DecodedJwt(compactJwt, payload, secretKey);
            var context = new ValidateJwtContext(secretKey, decodedJwt, propertyBag);
            await InvokeValidationHandlersAsync(context, parameters.Handlers, cancellationToken);

            // TODO
            var claimsIdentity = new ClaimsIdentity();

            return ValidateJwtResult.Success(decodedJwt, claimsIdentity, propertyBag);
        }
        catch (Exception exception)
        {
            return ValidateJwtResult.Fail(token, exception, propertyBag);
        }
    }

    private static async ValueTask<IReadOnlyCollection<SecretKey>> ResolveSecretKeysAsync(
        CompactJwt compactJwt,
        ValidateJwtParameters parameters,
        PropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        // TODO: add support for OpenID Connect Discovery/Metadata
        var secretKeys = await parameters.ResolveSecretKeysAsync(compactJwt, parameters, propertyBag, cancellationToken);
        var secretKeysList = secretKeys as IReadOnlyCollection<SecretKey> ?? secretKeys.ToList();
        // the degenerate case will attempt to use all the keys
        return secretKeysList.Count > 0 ? secretKeysList : parameters.SecretKeys;
    }

    private IReadOnlyDictionary<string, object> DeserializePayload(CompactJwt compactJwt, IEnumerable<SecretKey> secretKeys, out SecretKey secretKey)
    {
        var exceptions = new List<Exception>();
        var keysAttempted = new HashSet<string>();
        foreach (var secretKeyAttempt in secretKeys)
        {
            try
            {
                keysAttempted.Add(secretKeyAttempt.KeyId);
                var payload = JoseSerializer.Deserialize<IReadOnlyDictionary<string, object>>(compactJwt, secretKeyAttempt) ??
                              throw new InvalidOperationException();
                secretKey = secretKeyAttempt;
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

    private static async ValueTask InvokeValidationHandlersAsync(
        ValidateJwtContext context,
        IEnumerable<IValidateJwtHandler> handlers,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler.ValidateAsync(context, cancellationToken);
        }
    }
}
