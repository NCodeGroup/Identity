#region Copyright Preamble
//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Used to provide additional information about the Claim being requested.
/// </summary>
public interface IRequestClaim
{
    /// <summary>
    /// Gets a value indicating whether the Claim being requested is an Essential or Voluntary Claim.
    /// </summary>
    bool? Essential { get; }

    /// <summary>
    /// Gets a value that should be returned when the Claim is being requested.
    /// </summary>
    string? Value { get; }

    /// <summary>
    /// Gets a set of values that should be returned when the Claim is being requested.
    /// </summary>
    IEnumerable<string>? Values { get; }
}
