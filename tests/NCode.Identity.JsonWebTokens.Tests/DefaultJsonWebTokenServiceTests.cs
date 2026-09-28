#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.JsonWebTokens.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.JsonWebTokens;

public class DefaultJsonWebTokenServiceTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IJsonWebTokenService _service;
    private readonly SecretKey _signingKey;
    private readonly JoseSigningCredentials _signingCredentials;
    private readonly DateTimeOffset _utcNow = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public DefaultJsonWebTokenServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new TestTimeProvider(_utcNow));
        services.AddSecretsLibrary();
        services.AddJoseLibrary();
        services.AddJsonWebTokensLibrary();
        _serviceProvider = services.BuildServiceProvider();

        _service = _serviceProvider.GetRequiredService<IJsonWebTokenService>();

        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        _signingKey = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "test" },
            keyBytes
        );

        var algorithmProvider = _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>();
        if (
            !algorithmProvider.Collection.TryGetSignatureAlgorithm(
                "HS256",
                out var signatureAlgorithm
            )
        )
            throw new InvalidOperationException("HS256 signature algorithm not found.");

        _signingCredentials = new JoseSigningCredentials(_signingKey, signatureAlgorithm);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }

    private string EncodeToken(
        string issuer = "https://issuer.example",
        string audience = "https://audience.example",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        IEnumerable<Claim>? subjectClaims = null
    )
    {
        var now = _utcNow;
        var parameters = new EncodeJwtParameters
        {
            SigningCredentials = _signingCredentials,
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = notBefore ?? now.AddMinutes(-1),
            Expires = expires ?? now.AddHours(1),
            SubjectClaims = subjectClaims,
        };
        return _service.EncodeJwt(parameters);
    }

    #region EncodeJwt Tests

    [Fact]
    public void EncodeJwt_WhenSigned_ReturnsThreeSegmentCompactJws()
    {
        var token = EncodeToken();

        Assert.Equal(2, token.Count(c => c == '.'));
    }

    [Fact]
    public void EncodeJwt_WhenNoCredentials_ThrowsArgumentException()
    {
        var parameters = new EncodeJwtParameters();

        Assert.Throws<ArgumentException>(() => _service.EncodeJwt(parameters));
    }

    #endregion

    #region ValidateJwtAsync Success Tests

    [Fact]
    public async Task ValidateJwtAsync_WhenValidWithMatchingClaims_Succeeds()
    {
        var token = EncodeToken();

        var parameters = new ValidateJwtParameters()
            .UseValidationKeys([_signingKey])
            .ValidateIssuer("https://issuer.example")
            .ValidateAudience("https://audience.example")
            .ValidateTokenLifeTime();

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
        Assert.NotNull(result.DecodedJwt);
    }

    [Fact]
    public async Task ValidateJwtAsync_WhenSubjectClaims_ProducesClaimsIdentity()
    {
        var token = EncodeToken(
            subjectClaims: [new Claim("name", "Alice"), new Claim("role", "admin")]
        );

        var parameters = new ValidateJwtParameters().UseValidationKeys([_signingKey]);

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);

        var identity = await result.GetClaimsIdentityAsync(CancellationToken.None);
        Assert.True(identity.IsAuthenticated);
        Assert.Equal("Alice", identity.FindFirst("name")?.Value);
        Assert.Equal("admin", identity.FindFirst("role")?.Value);
    }

    #endregion

    #region ValidateJwtAsync Failure Tests

    [Fact]
    public async Task ValidateJwtAsync_WhenIssuerMismatch_Fails()
    {
        var token = EncodeToken(issuer: "https://wrong.example");

        var parameters = new ValidateJwtParameters()
            .UseValidationKeys([_signingKey])
            .ValidateIssuer("https://issuer.example");

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<TokenValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateJwtAsync_WhenExpired_Fails()
    {
        var past = _utcNow.AddHours(-2);
        var token = EncodeToken(notBefore: past.AddMinutes(-1), expires: past);

        var parameters = new ValidateJwtParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenLifeTime();

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<TokenValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateJwtAsync_WhenNoValidationKeys_FailsWithKeyNotFound()
    {
        var token = EncodeToken();

        var parameters = new ValidateJwtParameters().UseValidationKeys([]);

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<TokenValidationSecretKeyNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task ValidateJwtAsync_WhenTampered_FailsWithDecodeException()
    {
        var token = EncodeToken();
        var tampered = token[..^4] + "AAAA";

        var parameters = new ValidateJwtParameters().UseValidationKeys([_signingKey]);

        var result = await _service.ValidateJwtAsync(tampered, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<TokenValidationDecodeException>(result.Exception);
    }

    [Fact]
    public async Task ValidateJwtAsync_WhenGetClaimsIdentityOnFailedResult_Throws()
    {
        var token = EncodeToken();

        var parameters = new ValidateJwtParameters().UseValidationKeys([]);

        var result = await _service.ValidateJwtAsync(token, parameters, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await result.GetClaimsIdentityAsync(CancellationToken.None)
        );
    }

    #endregion
}
