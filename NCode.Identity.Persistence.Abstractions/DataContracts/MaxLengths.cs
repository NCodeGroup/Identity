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

namespace NCode.Identity.Persistence.DataContracts;

/// <summary>
/// Contains constants for the maximum lengths of various fields.
/// </summary>
public static class MaxLengths
{
    /// <summary>
    /// Specifies the maximum length of a concurrency token.
    /// </summary>
    public const int ConcurrencyToken = 50;

    /// <summary>
    /// Specifies the maximum length of a <c>TenantId</c>.
    /// </summary>
    public const int TenantId = 300;

    /// <summary>
    /// Specifies the maximum length of a tenant's <c>DomainName</c>.
    /// </summary>
    public const int TenantDomainName = 300;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretId</c>.
    /// </summary>
    public const int SecretId = 300;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretUse</c>.
    /// </summary>
    public const int SecretUse = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretAlgorithm</c>.
    /// </summary>
    public const int SecretAlgorithm = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretType</c>.
    /// </summary>
    public const int SecretType = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>ClientId</c>.
    /// </summary>
    public const int ClientId = 300;

    /// <summary>
    /// Specifies the maximum length of a <c>SubjectId</c>.
    /// </summary>
    public const int SubjectId = 300;

    /// <summary>
    /// Specifies the maximum length of a <c>GrantType</c>.
    /// </summary>
    public const int GrantType = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>HashedKey</c>.
    /// </summary>
    public const int HashedKey = 1000;

    /// <summary>
    /// Specifies the maximum length of a <c>UrlType</c>.
    /// </summary>
    public const int UrlType = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>UrlValue</c>.
    /// </summary>
    public const int UrlValue = 1000;
}
