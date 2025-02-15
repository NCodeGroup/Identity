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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationSource"/> abstraction.
/// </summary>
[PublicAPI]
public class AuthorizationSource :
    OpenIdMessage<AuthorizationSource>,
    IAuthorizationSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationSource"/> class.
    /// </summary>
    public AuthorizationSource()
    {
        // nothing
    }

    /// <inheritdoc />
    protected AuthorizationSource(AuthorizationSource other)
        : base(other)
    {
        AuthorizationSourceType = other.AuthorizationSourceType;
    }

    /// <inheritdoc />
    public AuthorizationSourceType AuthorizationSourceType { get; set; }

    /// <inheritdoc />
    public override AuthorizationSource Clone() => new(this);

    IAuthorizationSource ISupportClone<IAuthorizationSource>.Clone() => Clone();
}
