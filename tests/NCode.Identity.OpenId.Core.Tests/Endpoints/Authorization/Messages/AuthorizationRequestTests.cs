// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Messages;

public class AuthorizationRequestTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    private static Dictionary<string, KnownParameter> GetKnownParameters()
    {
        var dataSource = new DefaultCommonParameterDataSource(NullChangeToken.Singleton);
        var knownParameters = dataSource.Collection.ToDictionary(knownParameter =>
            knownParameter.Name
        );

        // Add authentication-specific parameters
        knownParameters[OpenIdAuthenticationParameters.Claims.Name] =
            OpenIdAuthenticationParameters.Claims;
        knownParameters[OpenIdAuthenticationParameters.RequestObjectSource.Name] =
            OpenIdAuthenticationParameters.RequestObjectSource;

        return knownParameters;
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
                new StringValuesJsonConverter(),
                new OpenIdMessageJsonConverterFactory(MockOpenIdEnvironment.Object),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
            },
        };

        MockOpenIdEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            AuthorizationSourceType = AuthorizationSourceTypes.Form,
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
            DPoPJwkThumbprint = "DPoPJwkThumbprint",
            DisplayType = "DisplayType",
            IdTokenHint = "IdTokenHint",
            LoginHint = "LoginHint",
            MaxAge = TimeSpan.FromMinutes(2.1),
            Nonce = "Nonce",
            PromptTypes = ["PromptType1", "PromptType2"],
            RedirectUri = "https://localhost/RedirectUri",
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
            DPoPJwkThumbprint = "DPoPJwkThumbprint",
            DisplayType = "DisplayType",
            IdTokenHint = "IdTokenHint",
            LoginHint = "LoginHint",
            MaxAge = TimeSpan.FromMinutes(2.1),
            Nonce = "Nonce",
            PromptTypes = ["PromptType1", "PromptType2"],
            RedirectUri = "https://localhost/RedirectUri",
            ResponseMode = "ResponseMode",
            ResponseTypes = ["ResponseType1", "ResponseType2"],
            Scopes = ["Scope1", "Scope2"],
            State = "State",
            UiLocales = ["UiLocale1", "UiLocale2"],
        };

        var authorizationRequest = new AuthorizationRequest(
            isContinuation,
            requestMessage,
            requestObject
        );

        var parameterCountBefore =
            authorizationRequest.OriginalRequestMessage.Parameters.Count()
            + authorizationRequest.OriginalRequestObject?.Parameters.Count();

        var json = JsonSerializer.Serialize(authorizationRequest, jsonSerializerOptions);
        Debug.WriteLine(json);

        var knownParameters = GetKnownParameters();

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns(
                (string parameterName) =>
                    knownParameters.TryGetValue(parameterName, out var knownParameter)
                        ? new ParameterDescriptor(knownParameter)
                        : new ParameterDescriptor(parameterName, ParameterLoader.Default)
            )
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x =>
                x.CreateMessage("AuthorizationRequestMessage", It.IsAny<IEnumerable<IParameter>>())
            )
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new AuthorizationRequestMessage(MockOpenIdEnvironment.Object, parameters)
            )
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x =>
                x.CreateMessage("AuthorizationRequestObject", It.IsAny<IEnumerable<IParameter>>())
            )
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new AuthorizationRequestObject(MockOpenIdEnvironment.Object, parameters)
            )
            .Verifiable();

        var result = JsonSerializer.Deserialize<IAuthorizationRequest>(json, jsonSerializerOptions);
        var parameterCountAfter =
            authorizationRequest.OriginalRequestMessage.Parameters.Count()
            + authorizationRequest.OriginalRequestObject?.Parameters.Count();

        Assert.IsType<AuthorizationRequest>(result);
        Assert.Same(MockOpenIdEnvironment.Object, result.OpenIdEnvironment);
        Assert.Equal(isContinuation, result.IsContinuation);
        Assert.Equal(parameterCountBefore, parameterCountAfter);

        // OriginalRequestMessage
        Assert.Same(MockOpenIdEnvironment.Object, result.OriginalRequestMessage.OpenIdEnvironment);
        Assert.Equal(serializationFormat, result.OriginalRequestMessage.SerializationFormat);
        AssertParameters(requestMessage, result.OriginalRequestMessage);
        Assert.Equal(
            requestMessage.AuthorizationSourceType,
            result.OriginalRequestMessage.AuthorizationSourceType
        );

        // OriginalRequestObject
        Assert.NotNull(result.OriginalRequestObject);
        Assert.Same(MockOpenIdEnvironment.Object, result.OriginalRequestObject.OpenIdEnvironment);
        Assert.Equal(serializationFormat, result.OriginalRequestObject.SerializationFormat);
        AssertParameters(requestObject, result.OriginalRequestObject);
        Assert.Equal(
            requestObject.AuthorizationSourceType,
            result.OriginalRequestObject.AuthorizationSourceType
        );
        Assert.Equal(
            requestObject.RequestObjectSource,
            result.OriginalRequestObject.RequestObjectSource
        );
    }

    [Fact]
    public void Serialize_RoundTrip_WhenTheRequestObjectIsReadFromAJwtPayload()
    {
        var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new StringValuesJsonConverter(),
                new OpenIdMessageJsonConverterFactory(MockOpenIdEnvironment.Object),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
            },
        };

        var knownParameters = GetKnownParameters();

        MockOpenIdEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(jsonSerializerOptions);

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns(
                (string parameterName) =>
                    knownParameters.TryGetValue(parameterName, out var knownParameter)
                        ? new ParameterDescriptor(knownParameter)
                        : new ParameterDescriptor(parameterName, ParameterLoader.Default)
            )
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x =>
                x.CreateMessage("AuthorizationRequestMessage", It.IsAny<IEnumerable<IParameter>>())
            )
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new AuthorizationRequestMessage(MockOpenIdEnvironment.Object, parameters)
            )
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x =>
                x.CreateMessage("AuthorizationRequestObject", It.IsAny<IEnumerable<IParameter>>())
            )
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new AuthorizationRequestObject(MockOpenIdEnvironment.Object, parameters)
            )
            .Verifiable();

        // a message without a type discriminator is created as a plain message
        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage("OpenIdMessage", It.IsAny<IEnumerable<IParameter>>()))
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new OpenIdMessage(MockOpenIdEnvironment.Object, parameters)
            );

        // the handler reads a request object from the payload of a JWT the same way
        var payload = JsonElement.Parse("""{"scope":"openid profile","nonce":"nonce-value"}""");
        var requestObject = payload.Deserialize<AuthorizationRequestObject>(jsonSerializerOptions);
        Assert.NotNull(requestObject);
        requestObject.RequestObjectSource = RequestObjectSource.Inline;

        // the handler serializes the request object like the request message beside it
        requestObject.SerializationFormat = SerializationFormat.Json;
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            AuthorizationSourceType = AuthorizationSourceTypes.Form,
            ClientId = "ClientId",
        };
        var authorizationRequest = new AuthorizationRequest(true, requestMessage, requestObject);

        var json = JsonSerializer.Serialize<IAuthorizationRequest>(
            authorizationRequest,
            jsonSerializerOptions
        );
        var result = JsonSerializer.Deserialize<IAuthorizationRequest>(json, jsonSerializerOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.OriginalRequestObject);
        Assert.Equal(
            requestObject.RequestObjectSource,
            result.OriginalRequestObject.RequestObjectSource
        );
        Assert.Equal(["openid", "profile"], result.OriginalRequestObject.Scopes);
        Assert.Equal("nonce-value", result.OriginalRequestObject.Nonce);
        AssertParameters(requestObject, result.OriginalRequestObject);
    }

    #region Request Object Values Tests

    [Fact]
    public void ResponseTypes_WhenTheRequestObjectOmitsThem_IgnoresTheOuterValue()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            ResponseTypes = [OpenIdProtocol.ResponseTypes.Code],
        };
        var requestObject = new AuthorizationRequestObject(MockOpenIdEnvironment.Object);
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, requestObject);

        var responseTypes = authorizationRequest.ResponseTypes;

        Assert.Empty(responseTypes);
    }

    [Fact]
    public void Scopes_WhenTheRequestObjectOmitsThem_IgnoresTheOuterValue()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            Scopes = [OpenIdProtocol.ScopeTypes.OpenId],
        };
        var requestObject = new AuthorizationRequestObject(MockOpenIdEnvironment.Object);
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, requestObject);

        var scopes = authorizationRequest.Scopes;

        Assert.Empty(scopes);
    }

    [Fact]
    public void Scopes_WhenTheOuterValueDiffers_ReturnsTheRequestObjectsValue()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            Scopes = [OpenIdProtocol.ScopeTypes.OpenId, "outer"],
        };
        var requestObject = new AuthorizationRequestObject(MockOpenIdEnvironment.Object)
        {
            Scopes = [OpenIdProtocol.ScopeTypes.OpenId],
        };
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, requestObject);

        var scopes = authorizationRequest.Scopes;

        Assert.Equal([OpenIdProtocol.ScopeTypes.OpenId], scopes);
    }

    [Fact]
    public void ClientId_WhenTheRequestObjectOmitsIt_ReturnsTheOuterValue()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            ClientId = "outer-client",
        };
        var requestObject = new AuthorizationRequestObject(MockOpenIdEnvironment.Object);
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, requestObject);

        var clientId = authorizationRequest.ClientId;

        Assert.Equal("outer-client", clientId);
    }

    #endregion

    #region ResponseMode Property Tests

    [Fact]
    public void ResponseMode_WhenNotRequested_ReturnsNull()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            ResponseTypes = [OpenIdProtocol.ResponseTypes.Code],
        };
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, null);

        var responseMode = authorizationRequest.ResponseMode;

        Assert.Null(responseMode);
    }

    [Fact]
    public void ResponseMode_WhenRequested_ReturnsTheRequestedMode()
    {
        var requestMessage = new AuthorizationRequestMessage(MockOpenIdEnvironment.Object)
        {
            ResponseTypes = [OpenIdProtocol.ResponseTypes.Code],
            ResponseMode = OpenIdProtocol.ResponseModes.FormPost,
        };
        var authorizationRequest = new AuthorizationRequest(false, requestMessage, null);

        var responseMode = authorizationRequest.ResponseMode;

        Assert.Equal(OpenIdProtocol.ResponseModes.FormPost, responseMode);
    }

    #endregion

    private static void AssertParameters(IOpenIdMessage original, IOpenIdMessage deserialized)
    {
        var environment = original.OpenIdEnvironment;
        Assert.Equal(original.Parameters.Count(), deserialized.Parameters.Count());
        foreach (var originalParameter in original.Parameters)
        {
            var parameterName = originalParameter.Descriptor.ParameterName;
            var contains = deserialized.Parameters.TryGet(
                parameterName,
                out var deserializedParameter
            );
            Assert.True(contains);
            Assert.NotNull(deserializedParameter);
            Assert.Equal(
                originalParameter.GetStringValues(environment),
                deserializedParameter.GetStringValues(environment)
            );
        }
    }
}
