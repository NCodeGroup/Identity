// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DataProtectionKeyRingReaderTests : BaseTests
{
    private static XElement KeyElement(string? expirationDate, params string[] values)
    {
        var key = new XElement("key", new XAttribute("id", Guid.NewGuid()));
        if (expirationDate is not null)
            key.Add(new XElement("expirationDate", expirationDate));

        key.Add(
            new XElement(
                "descriptor",
                values.Select(value => new XElement(
                    MasterKeyXmlFormat.EncryptedSecretName,
                    new XElement(MasterKeyXmlFormat.ValueName, value)
                ))
            )
        );
        return key;
    }

    private DataProtectionKeyRingReader CreateReader(params XElement[] elements)
    {
        var repository = CreateStrictMock<IXmlRepository>();
        repository.Setup(x => x.GetAllElements()).Returns(elements);
        return new DataProtectionKeyRingReader(
            Options.Create(new KeyManagementOptions { XmlRepository = repository.Object })
        );
    }

    [Fact]
    public void TryRead_WithoutARepository_ReturnsFalse()
    {
        var reader = new DataProtectionKeyRingReader(Options.Create(new KeyManagementOptions()));

        Assert.False(reader.TryRead(out var values));
        Assert.Empty(values);
    }

    [Fact]
    public void TryRead_ReturnsTheValueAndTheExpirationOfEveryKey()
    {
        var reader = CreateReader(
            KeyElement("2027-01-01T00:00:00+00:00", "value-1"),
            KeyElement("2027-02-01T00:00:00Z", "value-2", "value-3")
        );

        Assert.True(reader.TryRead(out var values));

        Assert.Equal(["value-1", "value-2", "value-3"], values.Select(x => x.ProtectedValue));
        Assert.Equal(
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            values[0].ExpirationDate
        );
    }

    [Fact]
    public void TryRead_IgnoresElementsThatAreNotKeys()
    {
        var revocation = new XElement(
            "revocation",
            new XElement(MasterKeyXmlFormat.ValueName, "not-a-key-value")
        );
        var reader = CreateReader(revocation, KeyElement(null, "value-1"));

        Assert.True(reader.TryRead(out var values));

        Assert.Equal("value-1", Assert.Single(values).ProtectedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a date")]
    public void TryRead_AnExpirationThatIsMissingOrUnparseable_IsUnknown(string? expirationDate)
    {
        var reader = CreateReader(KeyElement(expirationDate, "value-1"));

        Assert.True(reader.TryRead(out var values));

        var value = Assert.Single(values);
        Assert.Null(value.ExpirationDate);
        Assert.True(value.IsLive(DateTimeOffset.MaxValue.AddYears(-1), TimeSpan.Zero));
    }

    [Fact]
    public void IsLive_AKeyExpiredWithinTheGrace_IsLive()
    {
        var now = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var value = new KeyRingValue("v", now.AddDays(-10));

        Assert.True(value.IsLive(now, TimeSpan.FromDays(30)));
        Assert.False(value.IsLive(now, TimeSpan.FromDays(5)));
    }
}
