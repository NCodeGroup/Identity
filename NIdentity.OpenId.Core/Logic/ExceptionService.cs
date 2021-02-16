#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Logic
{
    internal interface IExceptionService
    {
        IHttpResult GetHttpResultForException(Exception exception);
    }

    internal class ExceptionService : IExceptionService
    {
        private const string DefaultErrorCode = OpenIdConstants.ErrorCodes.ServerError;
        private const int DefaultStatusCode = StatusCodes.Status500InternalServerError;

        private readonly ILogger<ExceptionService> _logger;
        private readonly IHttpResultFactory _httpResultFactory;

        public ExceptionService(ILogger<ExceptionService> logger, IHttpResultFactory httpResultFactory)
        {
            _logger = logger;
            _httpResultFactory = httpResultFactory;
        }

        public IHttpResult GetHttpResultForException(Exception exception)
        {
            if (exception is OpenIdException openIdException)
            {
                _logger.LogWarning(
                    "An OAuth/OpenID operation failed: StatusCode={StatusCode}; ErrorCode={ErrorCode}; ErrorDescription={ErrorDescription}; ErrorUri={ErrorUri}",
                    openIdException.ErrorDetails.StatusCode,
                    openIdException.ErrorDetails.Code,
                    openIdException.ErrorDetails.Description,
                    openIdException.ErrorDetails.Uri);
            }
            else
            {
                _logger.LogError(exception, "An unhandled exception occured");
                openIdException = OpenIdException.Factory.Create(DefaultErrorCode, exception);
            }

            var statusCode = openIdException.ErrorDetails.StatusCode ?? DefaultStatusCode;

            var httpResult = statusCode switch
            {
                StatusCodes.Status400BadRequest => _httpResultFactory.BadRequest(openIdException.ErrorDetails),
                StatusCodes.Status404NotFound => _httpResultFactory.NotFound(openIdException.ErrorDetails),
                _ => _httpResultFactory.Object(statusCode, openIdException.ErrorDetails)
            };

            return httpResult;
        }
    }
}
