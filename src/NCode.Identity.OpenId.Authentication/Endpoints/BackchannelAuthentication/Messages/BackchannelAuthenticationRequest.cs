// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IBackchannelAuthenticationRequest"/> abstraction.
/// </summary>
public sealed class BackchannelAuthenticationRequest
    : OpenIdMessage<BackchannelAuthenticationRequest>,
        IBackchannelAuthenticationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationRequest"/> class.
    /// </summary>
    public BackchannelAuthenticationRequest()
    {
        // nothing
    }

    private BackchannelAuthenticationRequest(BackchannelAuthenticationRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public List<string>? Scopes
    {
        get => GetKnownParameter(OpenIdCommonParameters.Scopes);
        set => SetKnownParameter(OpenIdCommonParameters.Scopes, value);
    }

    /// <inheritdoc />
    public List<string>? Resources
    {
        get => GetKnownParameter(OpenIdCommonParameters.Resources);
        set => SetKnownParameter(OpenIdCommonParameters.Resources, value);
    }

    /// <inheritdoc />
    public string? LoginHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.LoginHint);
        set => SetKnownParameter(OpenIdCommonParameters.LoginHint, value);
    }

    /// <inheritdoc />
    public string? LoginHintToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.LoginHintToken);
        set => SetKnownParameter(OpenIdCommonParameters.LoginHintToken, value);
    }

    /// <inheritdoc />
    public string? IdTokenHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdTokenHint);
        set => SetKnownParameter(OpenIdCommonParameters.IdTokenHint, value);
    }

    /// <inheritdoc />
    public string? BindingMessage
    {
        get => GetKnownParameter(OpenIdCommonParameters.BindingMessage);
        set => SetKnownParameter(OpenIdCommonParameters.BindingMessage, value);
    }

    /// <inheritdoc />
    public string? ClientNotificationToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.ClientNotificationToken);
        set => SetKnownParameter(OpenIdCommonParameters.ClientNotificationToken, value);
    }

    /// <inheritdoc />
    public string? RequestedExpiry
    {
        get => GetKnownParameter(OpenIdCommonParameters.RequestedExpiry);
        set => SetKnownParameter(OpenIdCommonParameters.RequestedExpiry, value);
    }

    /// <inheritdoc />
    public string? RequestJwt
    {
        get => GetKnownParameter(OpenIdCommonParameters.RequestJwt);
        set => SetKnownParameter(OpenIdCommonParameters.RequestJwt, value);
    }

    /// <inheritdoc />
    public override BackchannelAuthenticationRequest Clone() => new(this);
}
