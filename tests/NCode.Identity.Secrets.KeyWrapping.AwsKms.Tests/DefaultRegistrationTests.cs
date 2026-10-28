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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;
using NCode.Identity.Secrets.Persistence;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests;

public class DefaultRegistrationTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private static IConfiguration CreateConfiguration(
        params IEnumerable<(string Key, string? Value)> values
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => KeyValuePair.Create(x.Key, x.Value)))
            .Build();

    private static ServiceCollection AddAwsKms(
        IConfiguration configuration,
        IAwsKmsClientFactory clientFactory
    )
    {
        var serviceCollection = new ServiceCollection();

        // A strict mock without setups fails the test when anything asks for a client, which is how the tests see
        // that registering and resolving the provider never creates one.
        serviceCollection.AddSingleton(clientFactory);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddAwsKmsKeyWrapper(configuration);

        return serviceCollection;
    }

    #region AddAwsKmsKeyWrapper Tests

    [Fact]
    public async Task AddAwsKmsKeyWrapper_RegistersProviderInCatalogWithoutCreatingClient()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKms(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AwsKms:KeyArn", KeyArn)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IKeyWrapper>().ToList();

        Assert.Contains(rootKeyProviders, x => x.Name == KeyWrapperNames.AwsKms);
        Assert.Contains(rootKeyProviders, x => x is AwsKmsKeyWrapper);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAwsKmsKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKms(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AwsKms:KeyArnn", KeyArn)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AwsKmsKeyWrapperOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(AwsKmsKeyWrapperOptions.DefaultSectionName, exception.Message);
    }

    #endregion
}
