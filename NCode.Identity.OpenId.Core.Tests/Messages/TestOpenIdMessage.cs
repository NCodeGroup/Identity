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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Tests.Messages;

internal interface ITestOpenIdMessage : IOpenIdMessage
{
    // nothing
}

internal class TestOpenIdMessage : OpenIdMessage<TestOpenIdMessage>, ITestOpenIdMessage
{
    public TestOpenIdMessage()
    {
        // nothing
    }

    protected TestOpenIdMessage(TestOpenIdMessage other)
        : base(other)
    {
        // nothing
    }

    public TestOpenIdMessage(OpenIdEnvironment environment, params Parameter[] parameters)
        : base(environment, parameters)
    {
        // nothing
    }

    public TestOpenIdMessage(OpenIdEnvironment environment, IEnumerable<Parameter> parameters)
        : base(environment, parameters)
    {
        // nothing
    }

    public override TestOpenIdMessage Clone() => new(this);
}
