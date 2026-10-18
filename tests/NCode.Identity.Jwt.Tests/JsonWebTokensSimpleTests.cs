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
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.Jwt;

public class JsonWebTokensSimpleTests
{
    #region Exception Tests

    [Fact]
    public void JwtValidationException_DefaultConstructor_UsesDefaultMessage()
    {
        var exception = new JwtValidationException();

        Assert.Equal("Token validation failed.", exception.Message);
    }

    [Fact]
    public void JwtValidationException_WithMessage_UsesMessage()
    {
        var exception = new JwtValidationException("boom");

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void JwtValidationException_WithInner_PreservesInner()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new JwtValidationException("boom", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void JwtDecodeException_DefaultConstructor_UsesDefaultMessage()
    {
        var exception = new JwtDecodeException();

        Assert.Equal(JwtDecodeException.DefaultMessage, exception.Message);
    }

    [Fact]
    public void JwtDecodeException_WithMessageAndInner_Preserves()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new JwtDecodeException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void JwtSecretKeyNotFoundException_DefaultConstructor_HasMessage()
    {
        var exception = new JwtSecretKeyNotFoundException();

        Assert.Contains("No keys were provided", exception.Message);
    }

    [Fact]
    public void JwtSecretKeyNotFoundException_WithMessageAndInner_Preserves()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new JwtSecretKeyNotFoundException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    #endregion

    #region JwtEncodingParameters Tests

    [Fact]
    public void JwtEncodingParameters_Defaults_AreExpected()
    {
        var parameters = new JwtEncodingParameters();

        Assert.Equal("JWT", parameters.TokenType);
        Assert.True(parameters.AddKeyIdHeader);
        Assert.Null(parameters.SigningCredentials);
        Assert.Null(parameters.EncryptionCredentials);
    }

    #endregion

    #region JwtValidationParameters Tests

    [Fact]
    public void JwtValidationParameters_Defaults_AreExpected()
    {
        var parameters = new JwtValidationParameters();

        Assert.Equal("AuthenticationTypes.Federation", parameters.AuthenticationType);
        Assert.Equal(ClaimsIdentity.DefaultNameClaimType, parameters.NameClaimType);
        Assert.Equal(ClaimsIdentity.DefaultRoleClaimType, parameters.RoleClaimType);
        Assert.Empty(parameters.Requirements);
        Assert.Null(parameters.ValidationKeys);
        Assert.Null(parameters.ResolveValidationKeysAsync);
        Assert.Null(parameters.CreateClaimsIdentityAsync);
        Assert.Equal(TimeSpan.FromMinutes(5.0), parameters.ClockSkew);
    }

    [Fact]
    public void RequireAssertion_AppendsAndReturnsSameInstance()
    {
        var parameters = new JwtValidationParameters();

        var result = parameters.RequireAssertion((_, _) => ValueTask.FromResult(true));

        Assert.Same(parameters, result);
        Assert.Single(parameters.Requirements);
    }

    [Fact]
    public void UseValidationKeys_ReturnsSameInstance()
    {
        var parameters = new JwtValidationParameters();

        var result = parameters.UseValidationKeys([]);

        Assert.Same(parameters, result);
    }

    #endregion
}
