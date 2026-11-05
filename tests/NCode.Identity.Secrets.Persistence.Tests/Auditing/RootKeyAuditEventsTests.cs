// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Auditing;

public sealed class RootKeyAuditEventsTests
{
    private const string KeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static OwnerRootKeyInfo CreateRootKey(
        SecretOwner owner,
        string state = OwnerRootKeyStates.Active,
        string? failure = null
    ) => new(owner, "aws-kms", KeyReference, null, state, Now, Now, Now, null, failure);

    [Fact]
    public void Create_ForATenant_CarriesTheOwnerKeyAndTime()
    {
        var rootKey = CreateRootKey(SecretOwner.ForTenant("acme"));

        var auditEvent = RootKeyAuditEvents.Create(
            rootKey,
            RootKeyChanges.Set,
            RootKeyChangeReasons.Requested,
            AuditEventSources.Management,
            AuditActor.System,
            Now
        );

        Assert.Equal(Now, auditEvent.EventTime);
        Assert.Equal(7, auditEvent.EventId.Version);
        Assert.Equal(AuditEventSources.Management, auditEvent.EventSource);
        Assert.Equal("acme", auditEvent.TenantId);
        Assert.Same(AuditActor.System, auditEvent.Actor);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(RootKeyChanges.Set, auditEvent.Change);
        Assert.Equal(RootKeyChangeReasons.Requested, auditEvent.Reason);
        Assert.Equal(SecretOwnerTypes.Tenant, auditEvent.OwnerType);
        Assert.Equal("acme", auditEvent.OwnerId);
        Assert.Equal("aws-kms", auditEvent.KeyWrapperName);
        Assert.Equal(KeyReference, auditEvent.KeyReference);
        Assert.Equal(OwnerRootKeyStates.Active, auditEvent.State);
        Assert.Null(auditEvent.Failure);
    }

    [Fact]
    public void Create_ForAnOwnerThatIsNotATenant_HasNoTenantId()
    {
        var rootKey = CreateRootKey(SecretOwner.Platform);

        var auditEvent = RootKeyAuditEvents.Create(
            rootKey,
            RootKeyChanges.Set,
            RootKeyChangeReasons.Requested,
            AuditEventSources.OpenId,
            AuditActor.System,
            Now
        );

        Assert.Null(auditEvent.TenantId);
        Assert.Equal(SecretOwnerTypes.Platform, auditEvent.OwnerType);
    }

    [Fact]
    public void Create_Unavailable_IsAFailureThatCarriesTheFailureText()
    {
        var rootKey = CreateRootKey(
            SecretOwner.ForTenant("acme"),
            OwnerRootKeyStates.Unavailable,
            "AccessDenied: revoked"
        );

        var auditEvent = RootKeyAuditEvents.Create(
            rootKey,
            RootKeyChanges.Unavailable,
            RootKeyChangeReasons.ValidationFailed,
            AuditEventSources.OpenId,
            AuditActor.System,
            Now
        );

        Assert.Equal(AuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal("AccessDenied: revoked", auditEvent.Failure);
    }

    [Fact]
    public void Create_ChangeThatIsNotUnavailable_IsASuccessWithoutTheFailureText()
    {
        var rootKey = CreateRootKey(
            SecretOwner.ForTenant("acme"),
            OwnerRootKeyStates.Active,
            "an earlier failure"
        );

        var auditEvent = RootKeyAuditEvents.Create(
            rootKey,
            RootKeyChanges.Restored,
            RootKeyChangeReasons.ValidationSucceeded,
            AuditEventSources.OpenId,
            AuditActor.System,
            Now
        );

        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Null(auditEvent.Failure);
    }

    [Theory]
    [InlineData(RootKeyChanges.Unavailable, RootKeyChangeReasons.ValidationFailed)]
    [InlineData(RootKeyChanges.Restored, RootKeyChangeReasons.ValidationSucceeded)]
    [InlineData(RootKeyChanges.Set, RootKeyChangeReasons.ValidationSucceeded)]
    public void ForValidation_ByChange_IsTheValidationReason(string change, string expected)
    {
        Assert.Equal(expected, RootKeyChangeReasons.ForValidation(change));
    }
}
