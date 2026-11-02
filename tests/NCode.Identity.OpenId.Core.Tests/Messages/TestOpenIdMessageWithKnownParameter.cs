// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

internal class TestOpenIdMessageWithKnownParameter
    : OpenIdMessage<TestOpenIdMessageWithKnownParameter>
{
    public TestOpenIdMessageWithKnownParameter()
    {
        // nothing
    }

    protected TestOpenIdMessageWithKnownParameter(TestOpenIdMessageWithKnownParameter other)
        : base(other)
    {
        // nothing
    }

    public static KnownParameter<ITestNestedObject?> KnownParameter { get; } =
        new("test-nested-object", new JsonParser<ITestNestedObject?>())
        {
            AllowMissingStringValues = true,
        };

    public ITestNestedObject? TestNestedObject
    {
        get => GetKnownParameter(KnownParameter);
        set => SetKnownParameter(KnownParameter, value);
    }

    public override TestOpenIdMessageWithKnownParameter Clone() => new(this);
}
