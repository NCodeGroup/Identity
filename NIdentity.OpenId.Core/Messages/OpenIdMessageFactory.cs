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
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageFactory : IOpenIdMessageFactory
    {
        /// <inheritdoc />
        public IOpenIdRequest CreateRequest()
            => new OpenIdRequest();

        /// <inheritdoc />
        public bool TryLoadRequest(IEnumerable<KeyValuePair<string, OpenIdStringValues>> parameters, out ValidationResult<IOpenIdRequest> result)
            => TryLoad<IOpenIdRequest, OpenIdRequest>(parameters, out result);

        private static bool TryLoad<TService, TImplementation>(IEnumerable<KeyValuePair<string, OpenIdStringValues>> parameters, out ValidationResult<TService> result)
            where TService : IOpenIdRequest
            where TImplementation : TService, new()
        {
            var message = new TImplementation();
            if (!message.TryLoad(parameters, out var loadResult))
            {
                result = loadResult.As<TService>();
                return false;
            }

            result = ValidationResult.Factory.Success<TService>(message);
            return true;
        }
    }
}
