// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

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

    public void Initialize(
        OpenIdEnvironment openIdEnvironment,
        IEnumerable<IParameter> parameters,
        bool cloneParameters = false
    ) => throw new NotImplementedException();

    public IOpenIdMessage CloneMessage() => throw new NotImplementedException();
}
