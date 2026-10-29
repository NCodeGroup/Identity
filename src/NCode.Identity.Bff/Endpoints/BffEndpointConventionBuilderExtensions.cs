#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Provides the ability to mark minimal-API endpoints as local backend-for-frontend (BFF) APIs.
/// </summary>
[PublicAPI]
public static class BffEndpointConventionBuilderExtensions
{
    /// <typeparam name="TBuilder">The type of the endpoint convention builder.</typeparam>
    /// <param name="builder">The endpoint convention builder to mark.</param>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Marks the endpoint as a local BFF API by adding a <see cref="BffApiAttribute"/> to its metadata.
        /// </summary>
        /// <remarks>
        /// The marking requires the anti-forgery header, passes the Fetch Metadata check, requires a BFF session,
        /// and answers a challenge with a <c>401</c> problem and a forbid with a <c>403</c> problem.
        /// Controllers use the <see cref="BffApiAttribute"/> attribute instead.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public TBuilder AsBffApi()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var attribute = new BffApiAttribute();
            return builder.WithMetadata(attribute);
        }
    }
}
