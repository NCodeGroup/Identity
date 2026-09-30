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

using NCode.Identity.OpenId.ResourceServers;

namespace NCode.Identity.OpenId.Core.ResourceServers;

/// <summary>
/// Provides the reserved system resource server for the standard OpenID Connect identity scopes.
/// </summary>
internal class DefaultOpenIdIdentityResourceServerProvider : ISystemResourceServerProvider
{
    /// <summary>
    /// The reserved audience identifier for the OpenID Connect identity resource server.
    /// </summary>
    public const string Identifier = "urn:ncode:openid";

    /// <inheritdoc />
    public SystemResourceServerDescriptor GetDescriptor() =>
        new()
        {
            Identifier = Identifier,
            Name = "OpenID Connect",
            Scopes =
            [
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.OpenId,
                    Description = "Sign you in and issue an identity token.",
                },
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.Profile,
                    Description = "Access your profile information.",
                },
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.Email,
                    Description = "Access your email address.",
                },
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.Address,
                    Description = "Access your postal address.",
                },
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.Phone,
                    Description = "Access your phone number.",
                },
                new SystemScopeDescriptor
                {
                    Value = OpenIdConstants.ScopeTypes.OfflineAccess,
                    Description = "Maintain access while you are offline (refresh tokens).",
                },
            ],
        };
}
