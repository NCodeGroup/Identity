// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Tests;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? Authorization,
    string Accept,
    string? Body,
    string? DPoP
);
