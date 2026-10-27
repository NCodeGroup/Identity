#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
