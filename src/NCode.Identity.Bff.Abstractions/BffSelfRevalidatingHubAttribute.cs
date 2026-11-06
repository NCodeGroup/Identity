// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a BFF hub whose application signs a connection's user out itself when the session ends, such as the Blazor hub
/// whose circuit signs out. The BFF never closes such a connection: it does not watch it while idle, an invocation that
/// finds the session ended fails, and once the session ended the connection's invocations run without the session.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffSelfRevalidatingHubAttribute : Attribute;
