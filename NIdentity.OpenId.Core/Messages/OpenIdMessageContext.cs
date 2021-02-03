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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageContext : IOpenIdMessageContext
    {
        public ILogger Logger { get; }

        public JsonSerializerOptions JsonSerializerOptions { get; }

        public IList<ValidationResult> Errors { get; } = new List<ValidationResult>();

        // ReSharper disable once SuggestBaseTypeForParameter
        public OpenIdMessageContext(ILogger<OpenIdMessageContext> logger)
        {
            Logger = logger;
            JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,

                Converters =
                {
                    new RequestClaimJsonConverter(),
                    new RequestClaimsJsonConverter(),
                    new OpenIdMessageJsonConverter<AuthorizationRequestObject>(this)
                }
            };
        }
    }
}
