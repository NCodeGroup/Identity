// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;

namespace NCode.Identity.Secrets.Persistence;

internal sealed class TestDataProtectorFactory<T>(IDataProtector dataProtector)
    : IDataProtectorFactory<T>
{
    public IDataProtector CreateDataProtector() => dataProtector;
}
