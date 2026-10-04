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
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Provides the ability to map the registered endpoint-group hierarchy onto the HTTP request pipeline.
/// </summary>
[PublicAPI]
public static class EndpointRouteBuilderExtensions
{
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> instance to map the endpoints to.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps all the endpoint groups and providers that have been registered with the service provider.
        /// </summary>
        public void MapEndpointGroups()
        {
            endpoints
                .ServiceProvider.GetRequiredService<IEndpointTreeRouteBuilder>()
                .Map(endpoints);
        }
    }
}
