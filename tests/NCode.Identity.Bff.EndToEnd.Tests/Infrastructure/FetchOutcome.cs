// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// The outcome of a <c>fetch</c> call that a page makes.
/// </summary>
/// <param name="Status">The HTTP status code, or <c>0</c> when the browser blocked the call.</param>
/// <param name="Body">The response body, or empty when the browser blocked the call.</param>
/// <param name="Blocked">Whether the browser blocked the call, for example on a CORS failure.</param>
public sealed record FetchOutcome(int Status, string Body, bool Blocked);
