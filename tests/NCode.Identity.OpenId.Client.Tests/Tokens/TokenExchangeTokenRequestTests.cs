// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class TokenExchangeTokenRequestTests : BaseTests
{
    #region AddGrantParameters Tests

    [Fact]
    public void AddGrantParameters_WithAllMembers_AddsEveryParameter()
    {
        var request = new TokenExchangeTokenRequest("subject", "urn:subject-type")
        {
            ActorToken = "actor",
            ActorTokenType = "urn:actor-type",
            RequestedTokenType = "urn:requested-type",
            Audiences = ["a1", "a2"],
        };
        List<KeyValuePair<string, string>> parameters = [];

        request.AddGrantParameters(parameters);

        Assert.Equal(
            [
                new KeyValuePair<string, string>("subject_token", "subject"),
                new KeyValuePair<string, string>("subject_token_type", "urn:subject-type"),
                new KeyValuePair<string, string>("actor_token", "actor"),
                new KeyValuePair<string, string>("actor_token_type", "urn:actor-type"),
                new KeyValuePair<string, string>("requested_token_type", "urn:requested-type"),
                new KeyValuePair<string, string>("audience", "a1"),
                new KeyValuePair<string, string>("audience", "a2"),
            ],
            parameters
        );
    }

    [Fact]
    public void AddGrantParameters_WithOnlyTheSubject_AddsOnlyTheSubjectParameters()
    {
        var request = new TokenExchangeTokenRequest("subject", "urn:subject-type");
        List<KeyValuePair<string, string>> parameters = [];

        request.AddGrantParameters(parameters);

        Assert.Equal(
            [
                new KeyValuePair<string, string>("subject_token", "subject"),
                new KeyValuePair<string, string>("subject_token_type", "urn:subject-type"),
            ],
            parameters
        );
    }

    #endregion
}
