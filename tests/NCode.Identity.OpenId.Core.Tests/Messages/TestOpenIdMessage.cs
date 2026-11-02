// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

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
