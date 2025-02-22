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

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

internal class AuthorizationRequestMessage :
    BaseAuthorizationRequestMessage<AuthorizationRequestMessage>,
    IAuthorizationRequestMessage
{
    public AuthorizationRequestMessage()
    {
        // nothing
    }

    public AuthorizationRequestMessage(OpenIdEnvironment openIdEnvironment)
        : base(openIdEnvironment)
    {
        // nothing
    }

    public AuthorizationRequestMessage(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
        : base(openIdEnvironment, parameters, cloneParameters)
    {
        // nothing
    }

    public AuthorizationSourceType AuthorizationSourceType
    {
        get => GetKnownParameter(KnownParameters.AuthorizationSourceType);
        set => SetKnownParameter(KnownParameters.AuthorizationSourceType, value);
    }
}
