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

using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.Persistence.AwsKms.Facades;

/// <summary>
/// Provides the AWS Key Management Service client for a region, so the code that wraps and unwraps keys can be tested
/// without calling AWS.
/// </summary>
// See ADR-0228.
internal interface IAwsKmsClientFactory
{
    /// <summary>
    /// Gets the client for the specified region. The same client is returned for every call with the same region.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <returns>The <see cref="IAmazonKeyManagementService"/> that calls the KMS endpoint of the region.</returns>
    IAmazonKeyManagementService GetClient(string region);
}
