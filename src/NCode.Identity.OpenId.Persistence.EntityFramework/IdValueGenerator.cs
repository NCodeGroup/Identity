// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
internal class IdValueGenerator(IIdGenerator<long> idGenerator) : ValueGenerator<long>
{
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    public override long Next(EntityEntry entry) => IdGenerator.CreateId();
}
