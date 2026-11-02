// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Core.Tests.Messages;

internal interface ITestNestedObject
{
    string? NestedPropertyName1 { get; set; }
}

internal class TestNestedObject : ITestNestedObject
{
    public string? NestedPropertyName1 { get; set; }
}
