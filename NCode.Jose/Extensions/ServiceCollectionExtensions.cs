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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.AuthenticatedEncryption.DataSources;
using NCode.Jose.Algorithms.Compression.DataSources;
using NCode.Jose.Algorithms.DataSources;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.KeyManagement.DataSources;
using NCode.Jose.Algorithms.Signature.DataSources;

namespace NCode.Jose.Extensions;

/// <summary>
/// Provides extension methods for adding Jose services and algorithms to the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Jose services and algorithms to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJose(this IServiceCollection services) =>
        services.AddJose(_ => { });

    /// <summary>
    /// Adds Jose services and algorithms to the specified <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">The action used to configure the <see cref="JoseOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJose(this IServiceCollection services, Action<JoseOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAddSingleton<IAesKeyWrap, AesKeyWrap>();
        services.TryAddSingleton<IAlgorithmProvider, AlgorithmProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlgorithmFilter, AlgorithmFilter>());

        services
            // digital signature
            .AddAlgorithmDataSource<NoneSignatureAlgorithmDataSource>()
            .AddAlgorithmDataSource<HmacSignatureAlgorithmDataSource>()
            .AddAlgorithmDataSource<RsaSignatureAlgorithmDataSource>()
            .AddAlgorithmDataSource<EccSignatureAlgorithmDataSource>()
            // key management
            .AddAlgorithmDataSource<DirectKeyManagementAlgorithmDataSource>()
            .AddAlgorithmDataSource<RsaKeyManagementAlgorithmDataSource>()
            .AddAlgorithmDataSource<AesKeyManagementAlgorithmDataSource>()
            .AddAlgorithmDataSource<EccKeyManagementAlgorithmDataSource>()
            .AddAlgorithmDataSource<Pbes2KeyManagementAlgorithmDataSource>()
            // authenticated encryption
            .AddAlgorithmDataSource<AesAuthenticatedEncryptionAlgorithmDataSource>()
            // compression
            .AddAlgorithmDataSource<CompressionAlgorithmDataSource>();

        services.TryAddSingleton<IJoseSerializer, JoseSerializer>();

        return services;
    }

    private static IServiceCollection AddAlgorithmDataSource<T>(
        this IServiceCollection services) where T : class, IAlgorithmDataSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlgorithmDataSource, T>());
        return services;
    }
}
