#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IClientAuthenticationService"/> abstraction.
/// </summary>
public class DefaultClientAuthenticationService(
    IOpenIdErrorFactory errorFactory,
    IEnumerable<IClientAuthenticationHandler> handlers
) : IClientAuthenticationService
{
    private IOpenIdError ErrorMultipleAuthMethods { get; } = errorFactory
        .InvalidRequest("Multiple client authentication methods were provided.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private IEnumerable<IClientAuthenticationHandler> Handlers { get; } = handlers;

    private ClientAuthenticationResult? ResultOrDefault { get; set; }

    /// <inheritdoc />
    public async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        return ResultOrDefault ??= await AuthenticateCoreAsync(openIdContext, cancellationToken);
    }

    private async ValueTask<ClientAuthenticationResult> AuthenticateCoreAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        // Requirements:
        // - Error if multiple results with different client IDs
        // - Error if multiple confidential clients
        // - Return first confidential client, if any
        // - Otherwise return first public client

        var capacity = Handlers.TryGetNonEnumeratedCount(out var count) ? count : 5;
        var results = new List<ClientAuthenticationResult>(capacity);

        foreach (var handler in Handlers)
        {
            var result = await handler.AuthenticateClientAsync(openIdContext, cancellationToken);

            if (result.IsUndefined)
                continue;

            if (result.IsError)
                return result;

            Debug.Assert(result.HasClient);

            if (results.Count > 0 && !string.Equals(result.Client.ClientId, results[0].Client!.ClientId, StringComparison.Ordinal))
            {
                return new ClientAuthenticationResult(ErrorMultipleAuthMethods);
            }

            results.Add(result);
        }

        if (results.Count == 0)
            return ClientAuthenticationResult.Undefined;

        if (results.Count == 1)
            return results[0];

        var confidentialResults = results.Where(result => result.IsConfidential).ToList();

        if (confidentialResults.Count > 1)
            return new ClientAuthenticationResult(ErrorMultipleAuthMethods);

        if (confidentialResults.Count == 1)
            return confidentialResults[0];

        return results[0];
    }
}
