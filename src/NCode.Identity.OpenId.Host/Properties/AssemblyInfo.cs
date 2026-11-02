// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

// Allows the integration test project to reference the host's internal types and its top-level
// Program entry point when booting the WebApplicationFactory.
[assembly: InternalsVisibleTo("NCode.Identity.OpenId.Integration.Tests")]
