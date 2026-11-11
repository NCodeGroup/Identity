// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationRequestMessage"/> abstraction.
/// </summary>
public class AuthorizationRequestMessage
    : BaseAuthorizationRequestMessage<AuthorizationRequestMessage>,
        IAuthorizationRequestMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequestMessage"/> class.
    /// </summary>
    public AuthorizationRequestMessage()
    {
        // nothing
    }

    /// <inheritdoc />
    protected AuthorizationRequestMessage(AuthorizationRequestMessage other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public AuthorizationRequestMessage(OpenIdEnvironment openIdEnvironment)
        : base(openIdEnvironment)
    {
        // nothing
    }

    /// <inheritdoc />
    public AuthorizationRequestMessage(
        OpenIdEnvironment openIdEnvironment,
        IEnumerable<IParameter> parameters,
        bool cloneParameters = false
    )
        : base(openIdEnvironment, parameters, cloneParameters)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? AuthorizationSourceType
    {
        get => GetKnownParameter(OpenIdCommonParameters.AuthorizationSourceType);
        set => SetKnownParameter(OpenIdCommonParameters.AuthorizationSourceType, value);
    }

    /// <inheritdoc />
    public override AuthorizationRequestMessage Clone() => new(this);

    IAuthorizationRequestMessage ISupportClone<IAuthorizationRequestMessage>.Clone() => Clone();
}
