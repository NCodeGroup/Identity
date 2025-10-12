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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdError"/> abstraction.
/// </summary>
[PublicAPI]
public class OpenIdError : OpenIdMessage<OpenIdError>, IOpenIdError, ISupportOpenIdError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdError"/> class.
    /// </summary>
    public OpenIdError()
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdError(OpenIdError other)
        : base(other)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdError"/> class with the specified <see cref="OpenIdEnvironment"/> and error code.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> associated with the current instance.</param>
    /// <param name="errorCode">The value for the <c>error</c> parameter.</param>
    public OpenIdError(OpenIdEnvironment openIdEnvironment, string errorCode)
        : base(openIdEnvironment)
    {
        Code = errorCode;
    }

    IOpenIdError ISupportOpenIdError.Error => this;

    /// <inheritdoc />
    public int? StatusCode { get; set; }

    /// <inheritdoc />
    public Exception? Exception { get; set; }

    /// <inheritdoc />
    public string Code
    {
        get => GetKnownParameter(CoreParameters.ErrorCode) ?? OpenIdConstants.ErrorCodes.ServerError;
        set => SetKnownParameter(CoreParameters.ErrorCode, value);
    }

    /// <inheritdoc />
    public string? Description
    {
        get => GetKnownParameter(CoreParameters.ErrorDescription);
        set => SetKnownParameter(CoreParameters.ErrorDescription, value);
    }

    /// <inheritdoc />
    public Uri? Uri
    {
        get => GetKnownParameter(CoreParameters.ErrorUri);
        set => SetKnownParameter(CoreParameters.ErrorUri, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(CoreParameters.State);
        set => SetKnownParameter(CoreParameters.State, value);
    }

    /// <inheritdoc />
    public override OpenIdError Clone() => new(this);
}
