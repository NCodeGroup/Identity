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

using System.Text.Json;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class ProtocolErrorReaderTests
{
    #region TryRead Tests

    [Fact]
    public void TryRead_WithNonStandardStatusCode_PreservesTheStatusCode()
    {
        const int statusCode = 499;
        var response = new ProviderHttpResponse(
            statusCode,
            JsonDocument.Parse("""{"error":"x"}""").RootElement
        );

        var result = ProtocolErrorReader.TryRead(response, out var error);

        Assert.True(result);
        Assert.NotNull(error);
        Assert.Equal(statusCode, error.StatusCode);
    }

    #endregion
}
