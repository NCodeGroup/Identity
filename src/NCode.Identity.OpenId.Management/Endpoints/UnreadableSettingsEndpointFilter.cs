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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. A request that needs a stored settings
/// document the registered settings cannot honor is answered <c>409 Conflict</c> with a problem that names the
/// document, so that the caller can replace it with a settings write.
/// </summary>
// See ADR-0245.
internal sealed class UnreadableSettingsEndpointFilter(
    ILogger<UnreadableSettingsEndpointFilter> logger
) : IEndpointFilter
{
    private ILogger<UnreadableSettingsEndpointFilter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        try
        {
            return await next(context);
        }
        catch (UnreadableSettingsException exception)
        {
            Logger.StoredSettingsUnreadable(
                exception.ScopeName,
                exception.OwnerId,
                exception.DocumentError
            );

            var detail = $"{exception.Message} Replace the document with a settings write.";
            var extensions = new Dictionary<string, object?>
            {
                ["settingsScope"] = exception.ScopeName,
                ["ownerId"] = exception.OwnerId,
                ["documentError"] = exception.DocumentError,
            };

            return TypedResults.Problem(
                detail: detail,
                statusCode: StatusCodes.Status409Conflict,
                extensions: extensions
            );
        }
    }
}
