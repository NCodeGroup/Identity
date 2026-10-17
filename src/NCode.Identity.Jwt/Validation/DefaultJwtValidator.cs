#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.Jwt.Claims;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.PropertyBag;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides a default implementation for the <see cref="IJwtValidator"/> interface. Requirements on the
/// <see cref="JwtValidationParameters"/> are evaluated by the registered <see cref="IJwtValidationHandler"/> strategies,
/// which vote on a shared <see cref="JwtValidationContext"/>; the token is valid only when every requirement succeeds
/// and nothing fails.
/// </summary>
/// <param name="serviceProvider">An <see cref="IServiceProvider"/> that can be used to resolve services.</param>
/// <param name="timeProvider">A <see cref="TimeProvider"/> that can be used to get the current time.</param>
/// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
/// <param name="secretKeyCollectionProvider">An <see cref="ISecretKeyCollectionProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
/// <param name="handlers">The registered <see cref="IJwtValidationHandler"/> strategies.</param>
internal sealed class DefaultJwtValidator(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IJoseSerializer joseSerializer,
    ISecretKeyCollectionProvider secretKeyCollectionProvider,
    IEnumerable<IJwtValidationHandler> handlers
) : IJwtValidator
{
    private static readonly CreateClaimsIdentityAsync DefaultCreateClaimsIdentity = (
        decodedJwt,
        _,
        authenticationType,
        nameClaimType,
        roleClaimType,
        _
    ) =>
        ValueTask.FromResult(
            DefaultClaimsIdentityFactory.CreateClaimsIdentity(
                authenticationType,
                nameClaimType,
                roleClaimType,
                decodedJwt.Payload
            )
        );

    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private ISecretKeyCollectionProvider SecretKeyCollectionProvider { get; } =
        secretKeyCollectionProvider;
    private IReadOnlyCollection<IJwtValidationHandler> Handlers { get; } = [.. handlers];

    /// <inheritdoc />
    public async ValueTask<JwtValidationResult> ValidateAsync(
        string token,
        JwtValidationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        var propertyBag = parameters.PropertyBag.Clone();
        try
        {
            var joseToken = JoseSerializer.Parse(token);

            var validationKeys = await ResolveValidationKeysAsync(
                joseToken,
                parameters,
                propertyBag,
                cancellationToken
            );

            var payload = DeserializePayload(joseToken, validationKeys, out var secretKey);

            var decodedJwt = new DecodedJwt(joseToken, payload, secretKey);

            var context = new JwtValidationContext(
                secretKey,
                decodedJwt,
                parameters.Requirements,
                propertyBag,
                ServiceProvider,
                TimeProvider
            );

            foreach (var handler in Handlers)
            {
                await handler.HandleAsync(context, cancellationToken);
            }

            if (!context.HasSucceeded)
            {
                return JwtValidationResult.Fail(
                    parameters,
                    propertyBag,
                    token,
                    new JwtValidationException(BuildFailureReason(context))
                );
            }

            var createClaimsIdentity =
                parameters.CreateClaimsIdentityAsync ?? DefaultCreateClaimsIdentity;

            return JwtValidationResult.Success(
                parameters,
                propertyBag,
                decodedJwt,
                createClaimsIdentity
            );
        }
        catch (Exception exception)
        {
            return JwtValidationResult.Fail(parameters, propertyBag, token, exception);
        }
    }

    private async ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
        JoseToken joseToken,
        JwtValidationParameters parameters,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (parameters.ResolveValidationKeysAsync is { } resolve)
        {
            return await resolve(
                joseToken,
                propertyBag,
                SecretKeyCollectionProvider,
                cancellationToken
            );
        }

        if (parameters.ValidationKeys is { } explicitKeys)
        {
            return explicitKeys;
        }

        return DefaultValidationKeyResolver.ResolveValidationKeys(
            joseToken.ProtectionType,
            joseToken.Header,
            SecretKeyCollectionProvider.Collection
        );
    }

    private static string BuildFailureReason(JwtValidationContext context)
    {
        if (context.FailureReasons.Count > 0)
            return string.Join(" ", context.FailureReasons);

        return "One or more token validation requirements were not satisfied.";
    }

    private JsonElement DeserializePayload(
        JoseToken joseToken,
        IEnumerable<SecretKey> validationKeys,
        out SecretKey secretKey
    )
    {
        StringBuilder? exceptions = null;
        StringBuilder? attemptedKeys = null;

        foreach (var attemptedKey in validationKeys)
        {
            try
            {
                var payload = JoseSerializer.Deserialize<JsonElement>(joseToken, attemptedKey);

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
            throw new JwtSecretKeyNotFoundException();
        }

        Debug.Assert(exceptions != null);

        throw new JwtDecodeException(
            $"{JwtDecodeException.DefaultMessage}.\n\nToken:\n{joseToken}\n\nKeys attempted:\n{attemptedKeys}\nInner exceptions:\n{exceptions}"
        );
    }
}
