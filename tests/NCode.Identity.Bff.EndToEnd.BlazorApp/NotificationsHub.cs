// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.SignalR;

namespace NCode.Identity.Bff.EndToEnd.BlazorApp;

/// <summary>
/// A hub that only pushes, so its connections stay idle.
/// </summary>
public sealed class NotificationsHub : Hub;
