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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Validates the optional <c>If-Match</c> precondition against the client's current concurrency token.
/// </summary>
[PublicAPI]
internal class DefaultClientIfMatchHandler
    : ICommandHandler<ValidateUpdateClientCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateUpdateClientCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, client, ifMatch, disposition) = command;

        // short-circuit if an earlier precondition already failed
        if (disposition.HasError)
        {
            return ValueTask.CompletedTask;
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, client.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            disposition.Error ??= new ManagementError
            {
                StatusCode = StatusCodes.Status412PreconditionFailed,
                Detail = "The supplied If-Match token does not match the current client.",
            };
        }

        return ValueTask.CompletedTask;
    }
}
