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
using System.Linq;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    public static class OpenIdMessageExtensions
    {
        public static bool TryLoad(this IOpenIdMessage message, IEnumerable<KeyValuePair<string, string>> parameters, out ValidationResult result)
        {
            var newParameters = parameters.Select(kvp => KeyValuePair.Create(kvp.Key, new OpenIdStringValues(kvp.Value)));
            return message.TryLoad(newParameters, out result);
        }

        public static bool TryLoad(this IOpenIdMessage message, IEnumerable<KeyValuePair<string, StringValues>> parameters, out ValidationResult result)
        {
            var newParameters = parameters.Select(kvp => KeyValuePair.Create(kvp.Key, new OpenIdStringValues(kvp.Value.AsEnumerable())));
            return message.TryLoad(newParameters, out result);
        }

        public static bool TryLoad(this IOpenIdMessage message, IEnumerable<KeyValuePair<string, IEnumerable<string>>> parameters, out ValidationResult result)
        {
            var newParameters = parameters.Select(kvp => KeyValuePair.Create(kvp.Key, new OpenIdStringValues(kvp.Value)));
            return message.TryLoad(newParameters, out result);
        }
    }
}