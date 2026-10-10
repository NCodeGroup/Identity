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

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Encapsulates all OpenID-specific information about an individual OpenID request.
/// </summary>
/// <remarks>
/// Member convention: an <c>abstract</c> member is request state a concrete context supplies; a <c>virtual</c> member
/// (such as <see cref="ErrorFactory"/> or <see cref="GetSubjectId"/>) is behavior derived from that state with a correct
/// default, overridable when a host needs to. This split is by modeling intent, not SemVer compatibility.
/// </remarks>
[PublicAPI]
public abstract class OpenIdContext : IAsyncDisposable
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    public abstract HttpContext Http { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdEnvironment"/> associated with the current request.
    /// </summary>
    public abstract OpenIdEnvironment Environment { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdErrorFactory"/> instance that can be used to create error responses
    /// </summary>
    public virtual IOpenIdErrorFactory ErrorFactory => Environment.ErrorFactory;

    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> associated with the current request.
    /// </summary>
    public abstract OpenIdServer Server { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdTenant"/> associated with the current request.
    /// </summary>
    public abstract OpenIdTenant Tenant { get; }

    /// <summary>
    /// Gets the <see cref="IMediator"/> instance that is scoped to the current request.
    /// </summary>
    public abstract IMediator Mediator { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets the name of the endpoint associated with the current request.
    /// </summary>
    public abstract string EndpointName { get; }

    /// <summary>
    /// Extracts the subject id for an end-user from a <see cref="ClaimsPrincipal"/> using the resolved tenant's
    /// <c>subject_claim_types</c> setting (falling back to <see cref="OpenIdConstants.DefaultSubjectClaimTypes"/>)
    /// applied through the environment's host-replaceable extraction algorithm. This is the single seam every end-user
    /// surface uses so the subject is derived consistently.
    /// </summary>
    /// <param name="subject">The <see cref="ClaimsPrincipal"/> to search for the subject id.</param>
    /// <returns>The subject id if found; otherwise <c>null</c>.</returns>
    public virtual string? GetSubjectId(ClaimsPrincipal subject)
    {
        var subjectClaimTypes = Tenant.SettingsProvider.Collection.TryGetValue(
            OpenIdSettingKeys.SubjectClaimTypes,
            out var configured
        )
            ? configured
            : OpenIdConstants.DefaultSubjectClaimTypes;

        return Environment.GetSubjectId(subject, subjectClaimTypes);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected abstract ValueTask DisposeAsyncCore();
}
