// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Messages.Commands;

/// <summary>
/// Represents a mediator command that loads the raw <see cref="IRequestValues"/> for the current HTTP request,
/// combining its query-string and form parameters into the untyped input a strongly-typed request message is built
/// from.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
[PublicAPI]
public readonly record struct LoadRequestValuesCommand(OpenIdContext OpenIdContext)
    : ICommand<IRequestValues>;
