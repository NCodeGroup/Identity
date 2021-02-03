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

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Logic
{
    internal class AuthorizationLogic
    {
        private readonly IOpenIdMessageFactory _openIdMessageFactory;

        public AuthorizationLogic(IOpenIdMessageFactory openIdMessageFactory)
        {
            _openIdMessageFactory = openIdMessageFactory;
        }

        public bool TryLoadAuthorizationRequestMessage(
            HttpContext httpContext,
            out ValidationResult<IAuthorizationRequestMessage> result)
        {
            IEnumerable<KeyValuePair<string, StringValues>> source;
            if (HttpMethods.IsGet(httpContext.Request.Method))
            {
                source = httpContext.Request.Query;
            }
            else if (HttpMethods.IsPost(httpContext.Request.Method))
            {
                source = httpContext.Request.Form;
            }
            else
            {
                result = ValidationResult.Factory.Error<IAuthorizationRequestMessage>(
                    "TODO: error",
                    "TODO: errorDescription",
                    StatusCodes.Status405MethodNotAllowed);
                return false;
            }

            var context = _openIdMessageFactory.CreateContext();
            var message = _openIdMessageFactory.Create<IAuthorizationRequestMessage>(context);

            if (!message.TryLoad(source, out var loadResult))
            {
                result = loadResult.AsError<IAuthorizationRequestMessage>();
                return false;
            }

            result = ValidationResult.Factory.Success(message);
            return true;
        }
    }
}
