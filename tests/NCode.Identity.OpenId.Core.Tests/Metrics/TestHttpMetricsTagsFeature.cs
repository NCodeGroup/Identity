// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http.Features;

namespace NCode.Identity.OpenId.Core.Tests.Metrics;

/// <summary>
/// Captures the attributes a component adds to the request's <c>http.server.request.duration</c> measurement.
/// </summary>
internal sealed class TestHttpMetricsTagsFeature : IHttpMetricsTagsFeature
{
    public ICollection<KeyValuePair<string, object?>> Tags { get; } = [];

    public bool MetricsDisabled { get; set; }
}
