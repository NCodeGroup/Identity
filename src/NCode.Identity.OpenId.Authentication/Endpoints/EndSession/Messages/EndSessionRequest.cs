// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IEndSessionRequest"/> abstraction.
/// </summary>
public sealed class EndSessionRequest : OpenIdMessage<EndSessionRequest>, IEndSessionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EndSessionRequest"/> class.
    /// </summary>
    public EndSessionRequest()
    {
        // nothing
    }

    private EndSessionRequest(EndSessionRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? IdTokenHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdTokenHint);
        set => SetKnownParameter(OpenIdCommonParameters.IdTokenHint, value);
    }

    /// <inheritdoc />
    public string? ClientId
    {
        get => GetKnownParameter(OpenIdCommonParameters.ClientId);
        set => SetKnownParameter(OpenIdCommonParameters.ClientId, value);
    }

    /// <inheritdoc />
    public string? PostLogoutRedirectUri
    {
        get => GetKnownParameter(OpenIdCommonParameters.PostLogoutRedirectUri);
        set => SetKnownParameter(OpenIdCommonParameters.PostLogoutRedirectUri, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(OpenIdCommonParameters.State);
        set => SetKnownParameter(OpenIdCommonParameters.State, value);
    }

    /// <inheritdoc />
    public List<string>? UiLocales
    {
        get => GetKnownParameter(OpenIdCommonParameters.UiLocales);
        set => SetKnownParameter(OpenIdCommonParameters.UiLocales, value);
    }

    /// <inheritdoc />
    public override EndSessionRequest Clone() => new(this);
}
