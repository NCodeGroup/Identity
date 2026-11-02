// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Results;

/// <summary>
/// Provides the ability to produce an <see cref="IResult"/> through an optimized path.
/// </summary>
[PublicAPI]
public interface IResultProvider
{
    /// <summary>
    /// Produces an <see cref="IResult"/> instance through an optimized path.
    /// </summary>
    /// <returns>The <see cref="IResult"/> instance.</returns>
    IResult AsHttpResult();
}
