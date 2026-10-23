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

using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Continue;

public class ContinueUrlsTests
{
    #region GetStateOrDefault Tests

    [Fact]
    public void GetStateOrDefault_WhenTheUrlCarriesAState_ReturnsIt()
    {
        var result = ContinueUrls.GetStateOrDefault("https://op.example/oauth2/continue?state=abc");

        Assert.Equal("abc", result);
    }

    [Fact]
    public void GetStateOrDefault_WhenTheUrlHasNoQuery_ReturnsNull()
    {
        var result = ContinueUrls.GetStateOrDefault("/account/sessions");

        Assert.Null(result);
    }

    [Fact]
    public void GetStateOrDefault_WhenTheQueryHasNoState_ReturnsNull()
    {
        var result = ContinueUrls.GetStateOrDefault("/x?y=1");

        Assert.Null(result);
    }

    #endregion
}
