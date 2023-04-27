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

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Jose.KeyManagement;
using NCode.Jose.Signature;

namespace NCode.Jose;

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
    /// Adds Jose services and algorithms to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">The action used to configure the <see cref="JoseOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJose(this IServiceCollection services, Action<JoseOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAdd(ServiceDescriptor.Singleton<IAlgorithmProvider, AlgorithmProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlgorithmFilter, AlgorithmFilter>());

        // digital signature
        services
            .AddAlgorithm(_ => new NoneSignatureAlgorithm())
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha256, 256, HMACSHA256.TryHashData)
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha384, 384, HMACSHA384.TryHashData)
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha512, 512, HMACSHA512.TryHashData)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha256, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha384, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha512, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha256, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha384, HashAlgorithmName.SHA384, RSASignaturePadding.Pss)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha512, HashAlgorithmName.SHA512, RSASignaturePadding.Pss)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha256, HashAlgorithmName.SHA256)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha384, HashAlgorithmName.SHA384)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha512, HashAlgorithmName.SHA512);

        // TODO: key management
        services
            .AddAlgorithm(_ => new DirectKeyManagementAlgorithm());

        // TODO: authenticated encryption

        return services;
    }

    private static IServiceCollection AddAlgorithm(
        this IServiceCollection services,
        Func<IServiceProvider, IAlgorithm> factory) =>
        services.AddSingleton(factory);

    private static IServiceCollection AddKeyedHashSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        int signatureSizeBits,
        KeyedHashFunctionDelegate keyedHashFunction) =>
        services.AddAlgorithm(_ => new KeyedHashSignatureAlgorithm(code, signatureSizeBits, keyedHashFunction));

    private static IServiceCollection AddRsaSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        HashAlgorithmName hashAlgorithmName,
        RSASignaturePadding padding) =>
        services.AddAlgorithm(_ => new RsaSignatureAlgorithm(code, hashAlgorithmName, padding));

    private static IServiceCollection AddEccSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        HashAlgorithmName hashAlgorithmName) =>
        services.AddAlgorithm(_ => new EccSignatureAlgorithm(code, hashAlgorithmName));
}
