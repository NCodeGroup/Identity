#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

namespace NIdentity.OpenId.Endpoints.Discovery;

/// <summary>
/// Represents metadata used during <c>OAuth</c> or <c>OpenID Connect</c> authorization server metadata discovery.
/// If <see cref="SuppressDiscovery"/> is <c>true</c>, then the associated endpoint will not be used for metadata discovery.
/// </summary>
public interface ISuppressDiscoveryMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint should be used for metadata discovery.
    /// </summary>
    bool SuppressDiscovery { get; }
}

/// <summary>
/// Represents metadata used during <c>OAuth</c> or <c>OpenID Connect</c> authorization server metadata discovery.
/// If <see cref="SuppressDiscovery"/> is <c>true</c>, then the associated endpoint will not be used for metadata discovery.
/// </summary>
public class SuppressDiscoveryMetadata : ISuppressDiscoveryMetadata
{
    /// <inheritdoc />
    public bool SuppressDiscovery { get; init; }
}
