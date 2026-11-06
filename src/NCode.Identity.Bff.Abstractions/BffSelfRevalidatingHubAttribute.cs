// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a BFF hub whose application signs a connection's user out itself when the session ends, such as the Blazor hub
/// whose circuit signs out, so the BFF does not close an idle connection of the hub. An invocation still revalidates the
/// session and closes the connection when it ended.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffSelfRevalidatingHubAttribute : Attribute;
