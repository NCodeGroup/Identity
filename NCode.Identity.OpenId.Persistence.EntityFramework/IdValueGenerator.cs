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

using IdGen;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Provides a value generator for <see cref="long"/> values using <see cref="IIdGenerator{T}"/>.
/// </summary>
/// <remarks>
/// See the following article for more information:
/// https://medium.com/ingeniouslysimple/why-did-we-shift-away-from-database-generated-ids-7e0e54a49bb3
/// </remarks>
public class IdValueGenerator(
    IIdGenerator<long> idGenerator
) : ValueGenerator<long>
{
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    public override long Next(EntityEntry entry) => IdGenerator.CreateId();
}
