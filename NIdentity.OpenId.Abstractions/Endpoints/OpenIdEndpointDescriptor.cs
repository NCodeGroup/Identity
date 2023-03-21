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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Endpoints;

public class OpenIdEndpointDescriptor
{
    /// <summary>
    /// Gets or set the internal name for this <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or set the friendly name for this <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata for this <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public EndpointMetadataCollection Metadata { get; init; } = EndpointMetadataCollection.Empty;

    /// <summary>
    /// Stores arbitrary metadata properties associated with the <see cref="OpenIdEndpointDescriptor"/>.
    /// </summary>
    public IDictionary<object, object?> Properties { get; } = new Dictionary<object, object?>();
}