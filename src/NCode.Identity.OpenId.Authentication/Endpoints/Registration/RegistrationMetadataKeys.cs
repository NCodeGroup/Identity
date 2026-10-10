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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Contains the keys under which informational client metadata (RFC 7591 §2) that has no dedicated runtime setting is
/// stored within a client's persisted settings document, so a registration can be round-tripped by the registration
/// management endpoint.
/// </summary>
internal static class RegistrationMetadataKeys
{
    public const string TokenEndpointAuthMethod = "token_endpoint_auth_method";
    public const string ClientName = "client_name";
    public const string ClientUri = "client_uri";
    public const string LogoUri = "logo_uri";
    public const string Contacts = "contacts";
    public const string TosUri = "tos_uri";
    public const string PolicyUri = "policy_uri";
    public const string JwksUri = "jwks_uri";
    public const string Jwks = "jwks";
    public const string SoftwareId = "software_id";
    public const string SoftwareVersion = "software_version";
}
