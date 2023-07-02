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
using NCode.Jose.AuthenticatedEncryption;
using NCode.Jose.Compression;
using NCode.Jose.KeyManagement;
using NCode.Jose.Signature;

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
    /// Adds Jose services and algorithms to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">The action used to configure the <see cref="JoseOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJose(this IServiceCollection services, Action<JoseOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAdd(ServiceDescriptor.Singleton<IAesKeyWrap, AesKeyWrap>());
        services.TryAdd(ServiceDescriptor.Singleton<IAlgorithmProvider, AlgorithmProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlgorithmFilter, AlgorithmFilter>());

        // digital signature
        services
            .AddAlgorithm<NoneSignatureAlgorithm>()
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha256, signatureSizeBits: 256, HMACSHA256.TryHashData)
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha384, signatureSizeBits: 384, HMACSHA384.TryHashData)
            .AddKeyedHashSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha512, signatureSizeBits: 512, HMACSHA512.TryHashData)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha256, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha384, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha512, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha256, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha384, HashAlgorithmName.SHA384, RSASignaturePadding.Pss)
            .AddRsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSsaPssSha512, HashAlgorithmName.SHA512, RSASignaturePadding.Pss)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha256, HashAlgorithmName.SHA256)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha384, HashAlgorithmName.SHA384)
            .AddEccSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EcdsaSha512, HashAlgorithmName.SHA512);

        // key management
        services
            .AddAlgorithm<DirectKeyManagementAlgorithm>()
            .AddRsaKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.RsaPkcs1, RSAEncryptionPadding.Pkcs1)
            .AddRsaKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.RsaOaep, RSAEncryptionPadding.OaepSHA1)
            .AddRsaKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.RsaOaep256, RSAEncryptionPadding.OaepSHA256)
            .AddAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes128, kekSizeBits: 128)
            .AddAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes192, kekSizeBits: 192)
            .AddAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes256, kekSizeBits: 256)
            .AddAesGcmKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes128Gcm, cekSizeBits: 128)
            .AddAesGcmKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes192Gcm, cekSizeBits: 192)
            .AddAesGcmKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes256Gcm, cekSizeBits: 256)
            .AddAlgorithm<EcdhKeyManagementAlgorithm>()
            .AddEcdhWithAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.EcdhEsAes128, cekSizeBits: 128)
            .AddEcdhWithAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.EcdhEsAes192, cekSizeBits: 192)
            .AddEcdhWithAesKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.EcdhEsAes256, cekSizeBits: 256)
            .AddPbes2KeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
                HashAlgorithmName.SHA256,
                keySizeBits: 128,
                maxIterationCount: 310000)
            .AddPbes2KeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192,
                HashAlgorithmName.SHA384,
                keySizeBits: 192,
                maxIterationCount: 250000)
            .AddPbes2KeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256,
                HashAlgorithmName.SHA512,
                keySizeBits: 256,
                maxIterationCount: 120000);

        // authenticated encryption
        services
            .AddAesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
                HMACSHA256.TryHashData,
                cekSizeBits: 128)
            .AddAesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192CbcHmacSha384,
                HMACSHA384.TryHashData,
                cekSizeBits: 192)
            .AddAesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256CbcHmacSha512,
                HMACSHA512.TryHashData,
                cekSizeBits: 256)
            .AddAesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                cekSizeBits: 128)
            .AddAesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
                cekSizeBits: 192)
            .AddAesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                cekSizeBits: 256);

        // compression
        services
            .AddAlgorithm<NoneCompressionAlgorithm>()
            .AddAlgorithm<DeflateCompressionAlgorithm>();

        return services;
    }

    private static IServiceCollection AddAlgorithm<T>(
        this IServiceCollection services) where T : class, new() =>
        services.AddSingleton<T>();

    private static IServiceCollection AddAlgorithm(
        this IServiceCollection services,
        Func<IAlgorithm> factory) =>
        services.AddSingleton(factory);

    private static IServiceCollection AddAlgorithm(
        this IServiceCollection services,
        Func<IServiceProvider, IAlgorithm> factory) =>
        services.AddSingleton(factory);

    private static IServiceCollection AddKeyedHashSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        int signatureSizeBits,
        KeyedHashFunctionDelegate keyedHashFunction) =>
        services.AddAlgorithm(() => new KeyedHashSignatureAlgorithm(code, signatureSizeBits, keyedHashFunction));

    private static IServiceCollection AddRsaSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        HashAlgorithmName hashAlgorithmName,
        RSASignaturePadding padding) =>
        services.AddAlgorithm(() => new RsaSignatureAlgorithm(code, hashAlgorithmName, padding));

    private static IServiceCollection AddEccSignatureAlgorithm(
        this IServiceCollection services,
        string code,
        HashAlgorithmName hashAlgorithmName) =>
        services.AddAlgorithm(() => new EccSignatureAlgorithm(code, hashAlgorithmName));

    private static IServiceCollection AddRsaKeyManagementAlgorithm(
        this IServiceCollection services,
        string code,
        RSAEncryptionPadding padding) =>
        services.AddAlgorithm(() => new RsaKeyManagementAlgorithm(code, padding));

    private static IServiceCollection AddAesKeyManagementAlgorithm(
        this IServiceCollection services,
        string code,
        int kekSizeBits) =>
        services.AddAlgorithm(serviceProvider => new AesKeyManagementAlgorithm(
            serviceProvider.GetRequiredService<IAesKeyWrap>(),
            code,
            kekSizeBits));

    private static IServiceCollection AddEcdhWithAesKeyManagementAlgorithm(
        this IServiceCollection services,
        string code,
        int cekSizeBits) =>
        services.AddAlgorithm(serviceProvider => new EcdhWithAesKeyManagementAlgorithm(
            serviceProvider.GetRequiredService<IAesKeyWrap>(),
            code,
            cekSizeBits));

    private static IServiceCollection AddAesGcmKeyManagementAlgorithm(
        this IServiceCollection services,
        string code,
        int cekSizeBits) =>
        services.AddAlgorithm(() => new AesGcmKeyManagementAlgorithm(
            code,
            cekSizeBits));

    private static IServiceCollection AddPbes2KeyManagementAlgorithm(
        this IServiceCollection services,
        string code,
        HashAlgorithmName hashAlgorithmName,
        int keySizeBits,
        int maxIterationCount) =>
        services.AddAlgorithm(serviceProvider => new Pbes2KeyManagementAlgorithm(
            serviceProvider.GetRequiredService<IAesKeyWrap>(),
            code,
            hashAlgorithmName,
            keySizeBits,
            maxIterationCount));

    private static IServiceCollection AddAesCbcHmacAuthenticatedEncryptionAlgorithm(
        this IServiceCollection services,
        string code,
        KeyedHashFunctionDelegate keyedHashFunction,
        int cekSizeBits) =>
        services.AddAlgorithm(() => new AesCbcHmacAuthenticatedEncryptionAlgorithm(
            code,
            keyedHashFunction,
            cekSizeBits));

    private static IServiceCollection AddAesGcmAuthenticatedEncryptionAlgorithm(
        this IServiceCollection services,
        string code,
        int cekSizeBits) =>
        services.AddAlgorithm(() => new AesGcmAuthenticatedEncryptionAlgorithm(
            code,
            cekSizeBits));
}
