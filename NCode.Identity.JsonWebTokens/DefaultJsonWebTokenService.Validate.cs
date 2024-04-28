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

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens.Exceptions;
using NCode.Identity.Secrets;

namespace NCode.Identity.JsonWebTokens;

partial class DefaultJsonWebTokenService
{
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
                cancellationToken);

            var payload = DeserializePayload(
                compactJwt,
                validationKeys,
                out var secretKey);

            var decodedJwt = new DecodedJwt(
                compactJwt,
                payload,
                secretKey);

            var context = new ValidateJwtContext(
                secretKey,
                decodedJwt,
                propertyBag,
                ServiceProvider,
                SystemClock);

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
        StringBuilder? exceptions = null;
        StringBuilder? attemptedKeys = null;

        foreach (var attemptedKey in validationKeys)
        {
            try
            {
                var payload = JoseSerializer.Deserialize<JsonElement>(compactJwt, attemptedKey);

                secretKey = attemptedKey;
                return payload;
            }
            catch (Exception exception)
            {
                exceptions ??= new StringBuilder("Inner exceptions:\n");
                exceptions.Append(exception);
                exceptions.Append('\n');
            }

            attemptedKeys ??= new StringBuilder("Keys attempted:\n");
            attemptedKeys.Append(attemptedKey);
            attemptedKeys.Append('\n');
        }

        if (attemptedKeys is null)
        {
            throw new TokenValidationSecretKeyNotFoundException();
        }

        Debug.Assert(exceptions != null);

        // BEGIN EXAMPLE
        // Token validation failed. Unable to decode the token with any of the provided keys.
        //
        // Token:
        // A.B.C
        //
        // Keys attempted:
        // RsaSecretKey { KeyId = '0x1234', Tags = ['foo','bar'], Size = 1024 }
        //
        // Inner exceptions:
        // Foo
        // Bar
        // END EXAMPLE

        throw new TokenValidationDecodeException(
            $"{TokenValidationDecodeException.DefaultMessage}.\n\nToken:\n{compactJwt}\n\nKeys attempted:\n{attemptedKeys}\nInner exceptions:\n{exceptions}");
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
