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

using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Serialization;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdContextTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdErrorFactory> MockOpenIdErrorFactory { get; }

    public OpenIdContextTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var context = new OpenIdMessageContext(MockOpenIdErrorFactory.Object);

        Assert.Same(MockOpenIdErrorFactory.Object, context.ErrorFactory);

        Assert.True(context.JsonSerializerOptions.PropertyNameCaseInsensitive);
        Assert.Equal(JsonNamingPolicy.CamelCase, context.JsonSerializerOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, context.JsonSerializerOptions.NumberHandling);

        Assert.Equal(JsonCommentHandling.Skip, context.JsonSerializerOptions.ReadCommentHandling);
        Assert.True(context.JsonSerializerOptions.AllowTrailingCommas);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is OpenIdMessageJsonConverterFactory);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaim, RequestClaim>);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaims, RequestClaims>);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is AuthorizationRequestJsonConverter);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>);
    }
}
