#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdExceptionHandler"/> abstraction.
/// </summary>
public class DefaultOpenIdExceptionHandler : IOpenIdExceptionHandler
{
    /// <inheritdoc />
    public ValueTask<IResult> HandleExceptionAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var result = exception switch
        {
            HttpResultException httpResultException => httpResultException.HttpResult,
            OpenIdException openIdException => openIdException.Error.AsHttpResult(),
            _ => openIdEnvironment.ErrorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .WithException(exception)
                .AsHttpResult()
        };

        return ValueTask.FromResult(result);
    }
}
