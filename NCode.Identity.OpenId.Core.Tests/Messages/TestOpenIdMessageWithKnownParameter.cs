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

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Tests.Messages;

internal class TestOpenIdMessageWithKnownParameter : OpenIdMessage<TestOpenIdMessageWithKnownParameter>
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
        new("test-nested-object", new JsonParser<ITestNestedObject>())
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
