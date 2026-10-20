#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Moq;

namespace NCode.Time.Tests;

public sealed class TimeProviderExtensionsTests : IDisposable
{
    private static readonly DateTimeOffset UtcNow = new DateTimeOffset(
        2026,
        10,
        20,
        13,
        45,
        30,
        TimeSpan.Zero
    ).AddTicks(1_234_567);

    private static readonly DateTimeOffset UtcNowInSeconds = new(
        2026,
        10,
        20,
        13,
        45,
        30,
        TimeSpan.Zero
    );

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    private Mock<TimeProvider> MockTimeProvider { get; }

    public TimeProviderExtensionsTests()
    {
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
    }

    public void Dispose() => MockRepository.Verify();

    #region GetUtcNowWithPrecisionInSeconds Tests

    [Fact]
    public void GetUtcNowWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcNow()
    {
        var now = MockTimeProvider.Object.GetUtcNowWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds, now);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    #endregion

    #region GetTimestampWithPrecisionInSeconds Tests

    [Fact]
    public void GetTimestampWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcTicks()
    {
        var timestamp = MockTimeProvider.Object.GetTimestampWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds.UtcTicks, timestamp);
    }

    #endregion
}
