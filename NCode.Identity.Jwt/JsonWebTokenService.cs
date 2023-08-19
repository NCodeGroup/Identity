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

// https://datatracker.ietf.org/doc/html/rfc7519

public interface IJsonWebTokenService
{
    ValueTask<ValidateJwtResult> ValidateJwtAsync(
        string token,
        ValidateJwtParameters parameters,
        CancellationToken cancellationToken);
}

public class JsonWebTokenService : IJsonWebTokenService
{
    private IJoseSerializer JoseSerializer { get; }
    private ISecretKeyProvider SecretKeyProvider { get; }

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

            var validationKeys = await GetValidationKeysAsync(
                parameters,
                compactJwt,
                propertyBag,
                cancellationToken);

            var payload = DeserializePayload(
                compactJwt,
                validationKeys,
                out var secretKey);

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

    public ClaimsIdentity CreateClaimsIdentity(DecodedJwt decodedJwt, ValidateJwtParameters parameters)
    {
        // var nameType = ClaimTypes.Name;
        // var roleType = ClaimTypes.Role;
        // new ClaimsIdentity(authenticationType, nameType, roleType);

        throw new NotImplementedException();
    }

    private async ValueTask<IEnumerable<SecretKey>> GetValidationKeysAsync(
        ValidateJwtParameters parameters,
        CompactJwt compactJwt,
        PropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var candidateKeys = await parameters.ResolveProviderKeysAsync(
            compactJwt,
            propertyBag,
            SecretKeyProvider,
            cancellationToken);

        var secretKeyTags = await parameters.ResolveSecretKeyTagsAsync(
            compactJwt,
            propertyBag,
            cancellationToken);

        var validationKeys = await parameters.ResolveValidationKeysAsync(
            compactJwt,
            propertyBag,
            candidateKeys,
            secretKeyTags,
            cancellationToken);

        return validationKeys;
    }

    private IReadOnlyDictionary<string, object> DeserializePayload(
        CompactJwt compactJwt,
        IEnumerable<SecretKey> validationKeys,
        out SecretKey secretKey)
    {
        var exceptions = new List<Exception>();
        var keysAttempted = new HashSet<string>();
        foreach (var keyAttempt in validationKeys)
        {
            try
            {
                keysAttempted.Add(keyAttempt.KeyId);
                var payload = JoseSerializer.Deserialize<IReadOnlyDictionary<string, object>>(compactJwt, keyAttempt) ??
                              throw new InvalidOperationException();

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
