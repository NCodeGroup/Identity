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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageFactory : IOpenIdMessageFactory
    {
        private readonly ILogger<OpenIdMessageFactory> _logger;

        public OpenIdMessageFactory(ILogger<OpenIdMessageFactory> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public IOpenIdRequest CreateRequest()
            => new OpenIdRequest(_logger);

        /// <inheritdoc />
        public bool TryLoadRequest(IEnumerable<KeyValuePair<string, StringValues>> parameters, out ValidationResult<IOpenIdRequest> result)
            => TryLoad(CreateRequest(), parameters, out result);

        private static bool TryLoad<TMessage>(TMessage message, IEnumerable<KeyValuePair<string, StringValues>> parameters, out ValidationResult<TMessage> result)
            where TMessage : IOpenIdRequest
        {
            if (!message.TryLoad(parameters, out var loadResult))
            {
                result = loadResult.As<TMessage>();
                return false;
            }

            result = ValidationResult.Factory.Success(message);
            return true;
        }
    }
}
