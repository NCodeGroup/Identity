// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Randomness;

namespace NCode.Identity.Cose.Tests;

/// <summary>
/// A random source that returns the bytes it was given, in order, so that a test reproduces a published vector.
/// </summary>
internal sealed class QueueRandomSource : ICoseRandomSource
{
    private readonly Queue<byte[]> _queue = new();

    public void Enqueue(string hex) => _queue.Enqueue(Convert.FromHexString(hex));

    public int Remaining => _queue.Count;

    public void Fill(Span<byte> destination)
    {
        var bytes = _queue.Dequeue();
        Assert.Equal(bytes.Length, destination.Length);
        bytes.CopyTo(destination);
    }
}
