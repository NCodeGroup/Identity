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
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Serialization;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Serialization;

public class DelegatingJsonConverterTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdContext> MockOpenIdContext { get; }

    public DelegatingJsonConverterTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<OpenIdContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void AuthorizationRequestMessage_RoundTrip()
    {
        MockOpenIdContext
            .Setup(x => x.KnownParameters)
            .Returns(KnownParameterCollection.Default)
            .Verifiable();

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new OpenIdMessageJsonConverterFactory(MockOpenIdContext.Object),
                new DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>()
            }
        };

        var inputMessage = new AuthorizationRequestMessage();
        inputMessage.Initialize(MockOpenIdContext.Object, Array.Empty<Parameter>());

        inputMessage.AuthorizationSourceType = AuthorizationSourceType.Union;
        inputMessage.Nonce = "nonce";
        inputMessage.DisplayType = DisplayType.Popup;
        inputMessage.MaxAge = TimeSpan.FromMinutes(3.5);
        inputMessage.Scopes = new[] { "scope1", "scope2" };

        var json = JsonSerializer.Serialize(inputMessage, jsonSerializerOptions);

        var outputMessage = JsonSerializer.Deserialize<IAuthorizationRequestMessage>(json, jsonSerializerOptions);
        Assert.Equal(inputMessage.AuthorizationSourceType, outputMessage?.AuthorizationSourceType);
        Assert.Equal(inputMessage.Nonce, outputMessage?.Nonce);
        Assert.Equal(inputMessage.DisplayType, outputMessage?.DisplayType);
        Assert.Equal(inputMessage.MaxAge, outputMessage?.MaxAge);
        Assert.Equal(inputMessage.Scopes, outputMessage?.Scopes);
    }

    [Fact]
    public void AuthorizationRequest_RoundTrip()
    {
        MockOpenIdContext
            .Setup(x => x.KnownParameters)
            .Returns(KnownParameterCollection.Default)
            .Verifiable();

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new OpenIdMessageJsonConverterFactory(MockOpenIdContext.Object),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>(),
                new DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>()
            }
        };

        var requestMessage = AuthorizationRequestMessage.Load(MockOpenIdContext.Object, Array.Empty<Parameter>());
        requestMessage.AuthorizationSourceType = AuthorizationSourceType.Query;
        requestMessage.Nonce = "!nonce!";
        requestMessage.DisplayType = DisplayType.Touch;
        requestMessage.MaxAge = TimeSpan.FromMinutes(5.0);
        requestMessage.Scopes = new[] { "!scope1!", "!scope2!" };
        requestMessage.RedirectUri = new Uri("https://localhost/test");

        var requestObject = AuthorizationRequestObject.Load(MockOpenIdContext.Object, Array.Empty<Parameter>());
        requestObject.RequestObjectSource = RequestObjectSource.Remote;
        requestObject.Nonce = "nonce";
        requestObject.DisplayType = DisplayType.Popup;
        requestObject.MaxAge = TimeSpan.FromMinutes(3.5);
        requestObject.Scopes = new[] { "scope1", "scope2" };

        var inputMessage = new AuthorizationRequest(requestMessage, requestObject);

        var json = JsonSerializer.Serialize(inputMessage, jsonSerializerOptions);

        var outputMessage = JsonSerializer.Deserialize<IAuthorizationRequest>(json, jsonSerializerOptions);

        Assert.Equal(inputMessage.OriginalRequestMessage.AuthorizationSourceType, outputMessage?.OriginalRequestMessage.AuthorizationSourceType);
        Assert.Equal(inputMessage.OriginalRequestObject?.AuthorizationSourceType, outputMessage?.OriginalRequestObject?.AuthorizationSourceType);

        Assert.Equal(requestObject.Nonce, outputMessage?.Nonce);
        Assert.Equal(requestObject.DisplayType, outputMessage?.DisplayType);
        Assert.Equal(requestObject.MaxAge, outputMessage?.MaxAge);
        Assert.Equal(requestObject.Scopes, outputMessage?.Scopes);

        Assert.Equal(requestMessage.RedirectUri, outputMessage?.RedirectUri);
    }
}
