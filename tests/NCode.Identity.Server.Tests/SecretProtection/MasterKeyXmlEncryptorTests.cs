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

using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class MasterKeyXmlEncryptorTests : SecretProtectionTestBase
{
    private const string ServerId = "server-a";

    private static readonly SecretOwner ServerOwner = SecretOwner.ForServer(ServerId);

    #region Helpers

    private async Task<ServerSecretProtectorHolder> CreateLoadedHolderAsync() =>
        new()
        {
            Protector = await GetProtectorAsync(CreateSecretServices(ServerOwner), ServerOwner),
        };

    private ServiceProvider CreateDecryptorServices(ServerSecretProtectorHolder holder)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(holder);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        ServiceProviders.Add(serviceProvider);
        return serviceProvider;
    }

    private ServiceProvider CreateDataProtectionServices(
        IXmlRepository repository,
        ServerSecretProtectorHolder holder
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(holder);
        serviceCollection.AddDataProtection().SetApplicationName("master-key-xml-encryptor-tests");
        serviceCollection.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = repository;
            options.XmlEncryptor = new MasterKeyXmlEncryptor(holder);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        ServiceProviders.Add(serviceProvider);
        return serviceProvider;
    }

    // An in-memory key ring repository that hands out copies, as a database would.
    private IXmlRepository CreateRepository(List<XElement> elements)
    {
        var repository = CreateStrictMock<IXmlRepository>();
        repository
            .Setup(x => x.GetAllElements())
            .Returns(() => elements.Select(element => new XElement(element)).ToList())
            .Verifiable();
        repository
            .Setup(x => x.StoreElement(It.IsAny<XElement>(), It.IsAny<string>()))
            .Callback<XElement, string>((element, _) => elements.Add(new XElement(element)))
            .Verifiable();
        return repository.Object;
    }

    private static XElement CreateKeyElement() =>
        new(
            "key",
            new XAttribute("id", "6d1f7c0e-0c62-4d6e-9a71-1d6a2f3d9b11"),
            new XElement("descriptor", new XElement("secret", "c2VjcmV0LWtleS1tYXRlcmlhbA==")),
            new XElement("creationDate", "2026-01-01T00:00:00Z")
        );

    #endregion

    #region Encrypt Tests

    [Fact]
    public async Task Encrypt_WhenProtectorIsLoaded_ReturnsElementThatNamesOwnerAndHoldsProtectedValue()
    {
        var holder = await CreateLoadedHolderAsync();
        var encryptor = new MasterKeyXmlEncryptor(holder);

        var info = encryptor.Encrypt(CreateKeyElement());

        Assert.Equal(typeof(MasterKeyXmlDecryptor), info.DecryptorType);
        Assert.Equal(MasterKeyXmlFormat.EncryptedSecretName, info.EncryptedElement.Name);
        Assert.Equal(
            ServerOwner.ToString(),
            (string?)info.EncryptedElement.Attribute(MasterKeyXmlFormat.OwnerName)
        );
        var value = (string?)info.EncryptedElement.Element(MasterKeyXmlFormat.ValueName);
        Assert.False(string.IsNullOrEmpty(value));
        Assert.DoesNotContain("c2VjcmV0LWtleS1tYXRlcmlhbA==", value);
    }

    [Fact]
    public void Encrypt_HolderNotLoaded_Throws()
    {
        var encryptor = new MasterKeyXmlEncryptor(new ServerSecretProtectorHolder());

        Assert.Throws<InvalidOperationException>(() => encryptor.Encrypt(CreateKeyElement()));
    }

    #endregion

    #region Decrypt Tests

    [Fact]
    public async Task EncryptThenDecrypt_RoundTripsElement()
    {
        var holder = await CreateLoadedHolderAsync();
        var encryptor = new MasterKeyXmlEncryptor(holder);
        var decryptor = new MasterKeyXmlDecryptor(CreateDecryptorServices(holder));
        var element = CreateKeyElement();

        var info = encryptor.Encrypt(element);
        var decrypted = decryptor.Decrypt(info.EncryptedElement);

        Assert.True(XNode.DeepEquals(element, decrypted));
    }

    [Fact]
    public async Task Decrypt_TamperedValue_Throws()
    {
        var holder = await CreateLoadedHolderAsync();
        var encryptor = new MasterKeyXmlEncryptor(holder);
        var decryptor = new MasterKeyXmlDecryptor(CreateDecryptorServices(holder));
        var info = encryptor.Encrypt(CreateKeyElement());
        var valueElement = info.EncryptedElement.Element(MasterKeyXmlFormat.ValueName);
        Assert.NotNull(valueElement);
        var value = valueElement.Value;
        var index = value.Length / 2;
        var tampered = value.ToCharArray();
        tampered[index] = value[index] == 'A' ? 'B' : 'A';
        valueElement.Value = new string(tampered);

        Assert.ThrowsAny<CryptographicException>(() => decryptor.Decrypt(info.EncryptedElement));
    }

    [Fact]
    public async Task Decrypt_ElementWithoutValue_Throws()
    {
        var holder = await CreateLoadedHolderAsync();
        var decryptor = new MasterKeyXmlDecryptor(CreateDecryptorServices(holder));
        var element = new XElement(MasterKeyXmlFormat.EncryptedSecretName);

        Assert.Throws<CryptographicException>(() => decryptor.Decrypt(element));
    }

    [Fact]
    public void Decrypt_HolderNotLoaded_Throws()
    {
        var decryptor = new MasterKeyXmlDecryptor(
            CreateDecryptorServices(new ServerSecretProtectorHolder())
        );
        var element = new XElement(MasterKeyXmlFormat.EncryptedSecretName);

        Assert.Throws<InvalidOperationException>(() => decryptor.Decrypt(element));
    }

    #endregion

    #region Data Protection Tests

    [Fact]
    public async Task DataProtection_SecondProviderReadsRingWrittenByFirst_Unprotects()
    {
        const string purpose = "master-key-xml-encryptor-tests";
        const string plaintext = "data protected by the first provider";
        var holder = await CreateLoadedHolderAsync();
        var elements = new List<XElement>();
        var repository = CreateRepository(elements);
        var first = CreateDataProtectionServices(repository, holder);
        var second = CreateDataProtectionServices(repository, holder);

        var protectedValue = first
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(purpose)
            .Protect(plaintext);
        var unprotected = second
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(purpose)
            .Unprotect(protectedValue);

        Assert.Equal(plaintext, unprotected);
        var key = Assert.Single(elements);
        Assert.NotNull(key.Descendants(MasterKeyXmlFormat.EncryptedSecretName).SingleOrDefault());
    }

    #endregion
}
