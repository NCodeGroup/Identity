// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationRequestObject"/> abstraction.
/// </summary>
public class AuthorizationRequestObject
    : BaseAuthorizationRequestMessage<AuthorizationRequestObject>,
        IAuthorizationRequestObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequestObject"/> class.
    /// </summary>
    public AuthorizationRequestObject()
    {
        // nothing
    }

    /// <inheritdoc />
    protected AuthorizationRequestObject(AuthorizationRequestObject other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public AuthorizationRequestObject(OpenIdEnvironment openIdEnvironment)
        : base(openIdEnvironment)
    {
        // nothing
    }

    /// <inheritdoc />
    public AuthorizationRequestObject(
        OpenIdEnvironment openIdEnvironment,
        IEnumerable<IParameter> parameters,
        bool cloneParameters = false
    )
        : base(openIdEnvironment, parameters, cloneParameters)
    {
        // nothing
    }

    /// <inheritdoc />
    public string AuthorizationSourceType => AuthorizationSourceTypes.Jar;

    /// <inheritdoc />
    public RequestObjectSource RequestObjectSource
    {
        get => GetKnownParameter(OpenIdAuthenticationParameters.RequestObjectSource);
        set => SetKnownParameter(OpenIdAuthenticationParameters.RequestObjectSource, value);
    }

    /// <inheritdoc />
    public override AuthorizationRequestObject Clone() => new(this);

    IAuthorizationRequestObject ISupportClone<IAuthorizationRequestObject>.Clone() => Clone();
}
