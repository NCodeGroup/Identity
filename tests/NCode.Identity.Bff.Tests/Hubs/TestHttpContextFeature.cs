// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;

namespace NCode.Identity.Bff.Tests.Hubs;

/// <summary>
/// An <see cref="IHttpContextFeature"/> that serves the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> that a test
/// assigns.
/// </summary>
internal sealed class TestHttpContextFeature : IHttpContextFeature
{
    /// <inheritdoc />
    public HttpContext? HttpContext { get; set; }
}
