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

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Holds the outcome of one retrieval of revocation information: the value it yielded and the time the value may be
/// reused until. The default value means that nothing usable was retrieved, which the
/// <see cref="RevocationRetrievalCache"/> remembers.
/// </summary>
/// <param name="Value">The value the retrieval yielded, or <see langword="null"/> when nothing usable was retrieved.</param>
/// <param name="CacheUntil">The time the value may be reused until, or <see langword="null"/> when it is not cached.</param>
/// <typeparam name="T">The type of the retrieved value.</typeparam>
internal readonly record struct RevocationRetrieval<T>(T? Value, DateTimeOffset? CacheUntil)
    where T : class;
