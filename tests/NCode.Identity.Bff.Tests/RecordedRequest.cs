// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tests;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? Authorization,
    string? Body,
    string? DPoP
);
