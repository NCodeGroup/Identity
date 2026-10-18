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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// The outcome of <see cref="IOutboundHttpContentReader.ReadCappedAsync"/>.
/// </summary>
[PublicAPI]
public enum OutboundHttpReadResult
{
    /// <summary>
    /// The entire response body was read into the destination and fit within the configured size cap.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// The response body exceeded the configured size cap; the read was abandoned and the destination holds only a
    /// prefix of the body.
    /// </summary>
    Exceeded = 1,
}
