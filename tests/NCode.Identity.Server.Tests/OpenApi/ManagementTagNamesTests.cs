// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NCode.Identity.OpenId;
using NCode.Identity.Server.OpenApi;
using Xunit;

namespace NCode.Identity.Server.Tests.OpenApi;

public class ManagementTagNamesTests
{
    [Fact]
    public void Names_HoldsEveryManagementEndpointTag()
    {
        var tags = typeof(OpenIdConstants.EndpointTags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(tag => tag != OpenIdConstants.EndpointTags.OpenId)
            .ToList();

        Assert.NotEmpty(tags);
        foreach (var tag in tags)
        {
            Assert.Contains(tag, (IReadOnlySet<string>)ManagementOpenApiTags.Names);
        }
    }

    [Fact]
    public void Names_DoesNotHoldTheOpenIdProtocolTag()
    {
        Assert.DoesNotContain(
            OpenIdConstants.EndpointTags.OpenId,
            (IReadOnlySet<string>)ManagementOpenApiTags.Names
        );
    }
}
