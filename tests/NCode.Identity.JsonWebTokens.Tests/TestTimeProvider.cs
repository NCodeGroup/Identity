#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

namespace NCode.Identity.JsonWebTokens;

// A deterministic wall-clock TimeProvider: GetTimestamp() returns UTC ticks with a 1-tick-per-100ns
// frequency, so GetTimestampWithPrecisionInSeconds() aligns with the DateTime.Ticks derived from
// nbf/exp claims (unlike the system provider's monotonic Stopwatch counter).
internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    public override long GetTimestamp() => utcNow.UtcDateTime.Ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
}
