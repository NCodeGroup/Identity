// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Provides extension methods that read the HTTP body logging metadata of an <see cref="Endpoint"/>.
/// </summary>
[PublicAPI]
public static class HttpBodyLoggingEndpointExtensions
{
    /// <param name="endpoint">The <see cref="Endpoint"/> instance, or <see langword="null"/> when none is selected.</param>
    extension(Endpoint? endpoint)
    {
        /// <summary>
        /// Gets a value indicating whether the endpoint is marked as one whose request or response body carries
        /// credentials or personal data, so HTTP logging never writes either body.
        /// </summary>
        public bool HasConfidentialBodies =>
            endpoint?.Metadata.GetMetadata<ConfidentialBodyMetadata>() is not null;

        /// <summary>
        /// Gets the body fields that HTTP logging may write for the endpoint: none when no endpoint is selected or
        /// when the endpoint has confidential bodies, otherwise the request and response bodies that the endpoint
        /// opts in to.
        /// </summary>
        public HttpLoggingFields LoggableBodyFields
        {
            get
            {
                if (endpoint is null || endpoint.HasConfidentialBodies)
                {
                    return HttpLoggingFields.None;
                }

                var fields = HttpLoggingFields.None;
                foreach (
                    var metadata in endpoint.Metadata.GetOrderedMetadata<HttpBodyLoggingMetadata>()
                )
                {
                    fields |= metadata.Fields;
                }

                return fields;
            }
        }
    }
}
