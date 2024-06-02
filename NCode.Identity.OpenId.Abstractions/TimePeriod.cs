#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId;

public readonly record struct TimePeriod
{
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public TimeSpan Duration { get; }

    public TimePeriod(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
        Duration = endTime - startTime;
    }

    public TimePeriod(DateTimeOffset startTime, TimeSpan duration)
    {
        StartTime = startTime;
        EndTime = startTime + duration;
        Duration = duration;
    }

    public void Deconstruct(out DateTimeOffset startTime, out DateTimeOffset endTime, out TimeSpan duration)
    {
        startTime = StartTime;
        endTime = EndTime;
        duration = Duration;
    }
}
