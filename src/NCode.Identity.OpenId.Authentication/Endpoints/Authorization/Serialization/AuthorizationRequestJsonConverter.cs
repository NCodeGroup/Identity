// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="IAuthorizationRequest"/>
/// instances to and from JSON.
/// </summary>
internal class AuthorizationRequestJsonConverter : JsonConverter<IAuthorizationRequest>
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IAuthorizationRequest).IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc />
    public override IAuthorizationRequest? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var envelope = JsonSerializer.Deserialize<AuthorizationRequestJsonEnvelope>(
            ref reader,
            options
        );

        if (envelope.OriginalRequestMessage is null)
            return null;

        var requestMessage = envelope.OriginalRequestMessage;
        Debug.Assert(requestMessage.OpenIdEnvironment != null);

        var requestObject = envelope.OriginalRequestObject;
        Debug.Assert(
            requestObject == null
                || requestObject.OpenIdEnvironment == requestMessage.OpenIdEnvironment
        );

        return new AuthorizationRequest(envelope.IsContinuation, requestMessage, requestObject)
        {
            GrantedScopes = envelope.GrantedScopes,
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        IAuthorizationRequest value,
        JsonSerializerOptions options
    )
    {
        var envelope = new AuthorizationRequestJsonEnvelope
        {
            IsContinuation = value.IsContinuation,
            GrantedScopes = value.GrantedScopes,
            OriginalRequestMessage = value.OriginalRequestMessage,
            OriginalRequestObject = value.OriginalRequestObject,
        };
        JsonSerializer.Serialize(writer, envelope, options);
    }
}
