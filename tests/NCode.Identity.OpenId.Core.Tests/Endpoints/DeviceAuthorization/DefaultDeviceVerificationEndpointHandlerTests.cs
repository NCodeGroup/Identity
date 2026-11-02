// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.DeviceAuthorization;

public class DefaultDeviceVerificationEndpointHandlerTests
{
    private const string TenantId = "tenant-1";

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private static PersistedGrant<DeviceCodeGrant> CreateGrant(string status) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = null,
            SessionId = null,
            Payload = new DeviceCodeGrant
            {
                ClientId = "client-1",
                Scopes = ["api"],
                Status = status,
            },
        };

    #region Approve Tests

    [Fact]
    public void Approve_WhenTheRequestIsPending_RecordsTheSubject()
    {
        var grant = CreateGrant(DeviceAuthorizationStatus.Pending);
        var subjectAuthentication = CreateSubjectAuthentication();

        var update = DefaultDeviceVerificationEndpointHandler.Approve(grant, subjectAuthentication);

        Assert.NotNull(update);
        Assert.Equal(DeviceAuthorizationStatus.Approved, update.Value.Payload.Status);
        Assert.Equal(subjectAuthentication, update.Value.Payload.SubjectAuthentication);
        Assert.Equal("client-1", update.Value.Payload.ClientId);
        Assert.Null(update.Value.ExpiresWhen);
    }

    [Fact]
    public void Approve_WhenTheRequestIsNoLongerPending_MakesNoChange()
    {
        var grant = CreateGrant(DeviceAuthorizationStatus.Denied);

        var update = DefaultDeviceVerificationEndpointHandler.Approve(
            grant,
            CreateSubjectAuthentication()
        );

        Assert.Null(update);
    }

    #endregion

    #region Deny Tests

    [Fact]
    public void Deny_WhenTheRequestIsPending_RecordsTheDenial()
    {
        var grant = CreateGrant(DeviceAuthorizationStatus.Pending);

        var update = DefaultDeviceVerificationEndpointHandler.Deny(grant);

        Assert.NotNull(update);
        Assert.Equal(DeviceAuthorizationStatus.Denied, update.Value.Payload.Status);
        Assert.Null(update.Value.Payload.SubjectAuthentication);
        Assert.Equal("client-1", update.Value.Payload.ClientId);
        Assert.Null(update.Value.ExpiresWhen);
    }

    [Fact]
    public void Deny_WhenTheRequestIsNoLongerPending_MakesNoChange()
    {
        var grant = CreateGrant(DeviceAuthorizationStatus.Approved);

        var update = DefaultDeviceVerificationEndpointHandler.Deny(grant);

        Assert.Null(update);
    }

    #endregion
}
