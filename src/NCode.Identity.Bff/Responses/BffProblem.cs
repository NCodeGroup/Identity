// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Responses;

/// <summary>
/// The <c>application/problem+json</c> body of a BFF API refusal.
/// </summary>
/// <param name="Type">
/// The problem type, one of the <see cref="BffProblemTypes"/> URNs.
/// </param>
/// <param name="Title">
/// The short, human-readable summary of the problem.
/// </param>
/// <param name="Status">
/// The HTTP status code of the response.
/// </param>
internal sealed record BffProblem(string Type, string Title, int Status);
