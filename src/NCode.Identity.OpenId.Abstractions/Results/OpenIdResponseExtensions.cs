#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides extension methods for <see cref="IOpenIdResponse"/>.
/// </summary>
[PublicAPI]
public static class OpenIdResponseExtensions
{
    extension<T>(T response)
        where T : class, IOpenIdResponse
    {
        /// <summary>
        /// Wraps the <see cref="IOpenIdResponse"/> in an HTTP <see cref="IResult"/>.
        /// </summary>
        /// <returns>The <see cref="IResult"/> instance.</returns>
        public IResult AsHttpResult()
        {
            // prevent metadata from being serialized into the HTTP response
            if (response is IOpenIdMessage message)
            {
                message.SerializationFormat = SerializationFormat.OpenId;
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            return response is IResultProvider resultProvider
                ? resultProvider.AsHttpResult()
                : new OpenIdResult<T>(response);
        }
    }
}
