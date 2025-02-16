#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Endpoints.Authorization.Messages;

public class AuthorizationRequestTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    private static Dictionary<string, KnownParameter> GetKnownParameters()
    {
        var dataSource = new DefaultKnownParameterDataSource(NullChangeToken.Singleton);
        return dataSource.Collection.ToDictionary(knownParameter => knownParameter.Name);
    }

    public AuthorizationRequestTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        const bool isContinuation = true;
        const SerializationFormat serializationFormat = SerializationFormat.Json;

        var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new OpenIdMessageJsonConverterFactory(MockOpenIdEnvironment.Object),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
            }
        };

        MockOpenIdEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            AuthorizationSourceType = AuthorizationSourceType.Form,
            //
            RequestJwt = "RequestJwt",
            RequestUri = new Uri("https://localhost/RequestUri"),
            //
            AcrValues = ["AcrValue1", "AcrValue2"],
            Claims = new RequestClaims
            {
                IdToken = new RequestClaimDictionary
                {
                    ["id"] = new RequestClaim
                    {
                        Essential = true,
                        Value = "id_value0",
                        Values = ["id_value1", "id_value2"],
                    },
                },
                UserInfo = new RequestClaimDictionary
                {
                    ["info"] = new RequestClaim
                    {
                        Essential = true,
                        Value = "info_value0",
                        Values = ["info_value1", "info_value2"],
                    },
                },
            },
            ClaimsLocales = ["ClaimsLocale1", "ClaimsLocale2"],
            ClientId = "ClientId",
            CodeChallenge = "CodeChallenge",
            CodeChallengeMethod = "CodeChallengeMethod",
            CodeVerifier = "CodeVerifier",
            DisplayType = "DisplayType",
            IdTokenHint = "IdTokenHint",
            LoginHint = "LoginHint",
            MaxAge = TimeSpan.FromMinutes(2.1),
            Nonce = "Nonce",
            PromptTypes = ["PromptType1", "PromptType2"],
            RedirectUri = new Uri("https://localhost/RedirectUri"),
            ResponseMode = "ResponseMode",
            ResponseTypes = ["ResponseType1", "ResponseType2"],
            Scopes = ["Scope1", "Scope2"],
            State = "State",
            UiLocales = ["UiLocale1", "UiLocale2"],
        };

        var requestObject = new AuthorizationRequestObject(MockOpenIdEnvironment.Object)
        {
            RequestObjectSource = RequestObjectSource.Remote,
            //
            AcrValues = ["AcrValue1", "AcrValue2"],
            Claims = new RequestClaims
            {
                IdToken = new RequestClaimDictionary
                {
                    ["id"] = new RequestClaim
                    {
                        Essential = true,
                        Value = "id_value0",
                        Values = ["id_value1", "id_value2"],
                    },
                },
                UserInfo = new RequestClaimDictionary
                {
                    ["info"] = new RequestClaim
                    {
                        Essential = true,
                        Value = "info_value0",
                        Values = ["info_value1", "info_value2"],
                    },
                },
            },
            ClaimsLocales = ["ClaimsLocale1", "ClaimsLocale2"],
            ClientId = "ClientId",
            CodeChallenge = "CodeChallenge",
            CodeChallengeMethod = "CodeChallengeMethod",
            CodeVerifier = "CodeVerifier",
            DisplayType = "DisplayType",
            IdTokenHint = "IdTokenHint",
            LoginHint = "LoginHint",
            MaxAge = TimeSpan.FromMinutes(2.1),
            Nonce = "Nonce",
            PromptTypes = ["PromptType1", "PromptType2"],
            RedirectUri = new Uri("https://localhost/RedirectUri"),
            ResponseMode = "ResponseMode",
            ResponseTypes = ["ResponseType1", "ResponseType2"],
            Scopes = ["Scope1", "Scope2"],
            State = "State",
            UiLocales = ["UiLocale1", "UiLocale2"],
        };

        var authorizationRequest = new AuthorizationRequest(
            isContinuation,
            requestMessage,
            requestObject);

        var parameterCountBefore =
            authorizationRequest.OriginalRequestMessage.Parameters.Count +
            authorizationRequest.OriginalRequestObject?.Parameters.Count;

        var json = JsonSerializer.Serialize(authorizationRequest, jsonSerializerOptions);
        Debug.WriteLine(json);

        var knownParameters = GetKnownParameters();

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns((string parameterName) =>
                knownParameters.TryGetValue(parameterName, out var knownParameter) ?
                    new ParameterDescriptor(knownParameter) :
                    new ParameterDescriptor(parameterName, ParameterLoader.Default)
            )
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage("AuthorizationRequestMessage", It.IsAny<IEnumerable<IParameter>>()))
            .Returns((string _, IEnumerable<IParameter> parameters) =>
                new AuthorizationRequestMessage(MockOpenIdEnvironment.Object, parameters))
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage("AuthorizationRequestObject", It.IsAny<IEnumerable<IParameter>>()))
            .Returns((string _, IEnumerable<IParameter> parameters) =>
                new AuthorizationRequestObject(MockOpenIdEnvironment.Object, parameters))
            .Verifiable();

        var result = JsonSerializer.Deserialize<IAuthorizationRequest>(json, jsonSerializerOptions);
        var parameterCountAfter =
            authorizationRequest.OriginalRequestMessage.Parameters.Count +
            authorizationRequest.OriginalRequestObject?.Parameters.Count;

        Assert.IsType<AuthorizationRequest>(result);
        Assert.Same(MockOpenIdEnvironment.Object, result.OpenIdEnvironment);
        Assert.Equal(isContinuation, result.IsContinuation);
        Assert.Equal(parameterCountBefore, parameterCountAfter);

        // OriginalRequestMessage
        Assert.Same(MockOpenIdEnvironment.Object, result.OriginalRequestMessage.OpenIdEnvironment);
        Assert.Equal(serializationFormat, result.OriginalRequestMessage.SerializationFormat);
        AssertParameters(requestMessage, result.OriginalRequestMessage);
        Assert.Equal(requestMessage.AuthorizationSourceType, result.OriginalRequestMessage.AuthorizationSourceType);

        // OriginalRequestObject
        Assert.NotNull(result.OriginalRequestObject);
        Assert.Same(MockOpenIdEnvironment.Object, result.OriginalRequestObject.OpenIdEnvironment);
        Assert.Equal(serializationFormat, result.OriginalRequestObject.SerializationFormat);
        AssertParameters(requestObject, result.OriginalRequestObject);
        Assert.Equal(requestObject.AuthorizationSourceType, result.OriginalRequestObject.AuthorizationSourceType);
        Assert.Equal(requestObject.RequestObjectSource, result.OriginalRequestObject.RequestObjectSource);
    }

    private static void AssertParameters(IOpenIdMessage original, IOpenIdMessage deserialized)
    {
        var environment = original.OpenIdEnvironment;
        Assert.Equal(original.Parameters.Count, deserialized.Parameters.Count);
        foreach (var (parameterName, originalParameter) in original.Parameters)
        {
            var deserializedParameter = Assert.Contains(parameterName, deserialized.Parameters);
            Assert.Equal(originalParameter.GetStringValues(environment), deserializedParameter.GetStringValues(environment));
        }
    }
}
