// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Messages;

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
