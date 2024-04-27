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

namespace NCode.Identity.OpenId.Data.Contracts;

/// <summary>
/// Provides the ability to check for optimistic concurrency violations by using a random value that compared to
/// the existing value in a database. The random value is automatically generated every time a row is inserted or
/// updated in the database.
/// </summary>
public interface ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets a random value that is used to check for optimistic concurrency violations.
    /// </summary>
    string ConcurrencyToken { get; set; }
}
