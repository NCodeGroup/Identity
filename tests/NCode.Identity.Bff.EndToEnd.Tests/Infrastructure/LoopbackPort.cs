// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Reserves free TCP ports on the loopback interface.
/// </summary>
public static class LoopbackPort
{
    /// <summary>
    /// Finds a free port on the loopback interface.
    /// </summary>
    public static int Reserve()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
