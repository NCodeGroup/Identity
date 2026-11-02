// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// The mediator command dispatched whenever an end-user session is terminated, for any reason (logout, idle timeout,
/// absolute expiry, concurrent-session eviction, administrative revocation, or global sign-out). It is the single
/// session-termination side-effect seam: the default handlers revoke the session's (or subject's) grants and send
/// back-channel logout notifications and audit records, and applications add handlers (for example, front-channel
/// logout or custom auditing) by registering additional <see cref="ICommandHandler{TCommand}"/> instances, ordered by
/// <see cref="ISupportMediatorPriority"/>.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Termination">The <see cref="SessionTermination"/> describing the cause and scope of the termination.</param>
[PublicAPI]
public readonly record struct OnSessionTerminatedCommand(
    OpenIdContext OpenIdContext,
    SessionTermination Termination
) : ICommand;
