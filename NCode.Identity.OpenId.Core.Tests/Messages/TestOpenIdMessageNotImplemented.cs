#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Tests.Messages;

internal class TestOpenIdMessageNotImplemented : ITestOpenIdMessage
{
    public OpenIdEnvironment OpenIdEnvironment => throw new NotImplementedException();

    public string TypeDiscriminator => throw new NotImplementedException();

    public SerializationFormat SerializationFormat
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public IParameterCollection Parameters => throw new NotImplementedException();

    public void Initialize(OpenIdEnvironment openIdEnvironment) =>
        throw new NotImplementedException();

    public void Initialize(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false) =>
        throw new NotImplementedException();

    public IOpenIdMessage CloneMessage() =>
        throw new NotImplementedException();
}
