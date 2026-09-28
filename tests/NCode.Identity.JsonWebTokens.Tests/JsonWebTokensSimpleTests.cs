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
using NCode.Identity.JsonWebTokens.Exceptions;

namespace NCode.Identity.JsonWebTokens;

public class JsonWebTokensSimpleTests
{
    #region Exception Tests

    [Fact]
    public void TokenValidationException_DefaultConstructor_UsesDefaultMessage()
    {
        var exception = new TokenValidationException();

        Assert.Equal("Token validation failed.", exception.Message);
    }

    [Fact]
    public void TokenValidationException_WithMessage_UsesMessage()
    {
        var exception = new TokenValidationException("boom");

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void TokenValidationException_WithInner_PreservesInner()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new TokenValidationException("boom", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void TokenValidationDecodeException_DefaultConstructor_UsesDefaultMessage()
    {
        var exception = new TokenValidationDecodeException();

        Assert.Equal(TokenValidationDecodeException.DefaultMessage, exception.Message);
    }

    [Fact]
    public void TokenValidationDecodeException_WithMessageAndInner_Preserves()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new TokenValidationDecodeException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void TokenValidationSecretKeyNotFoundException_DefaultConstructor_HasMessage()
    {
        var exception = new TokenValidationSecretKeyNotFoundException();

        Assert.Contains("No keys were provided", exception.Message);
    }

    [Fact]
    public void TokenValidationSecretKeyNotFoundException_WithMessageAndInner_Preserves()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new TokenValidationSecretKeyNotFoundException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    #endregion

    #region JsonWebTokensLibrary Tests

    [Fact]
    public void JsonWebTokensLibrary_ExposesMarkerMetadata()
    {
        var library = new JsonWebTokensLibrary();

        Assert.Equal("NCode.Identity.JsonWebTokens", library.DisplayName);
        Assert.Equal("AddJsonWebTokensLibrary", library.ConfigureMethod);
    }

    #endregion

    #region EncodeJwtParameters Tests

    [Fact]
    public void EncodeJwtParameters_Defaults_AreExpected()
    {
        var parameters = new EncodeJwtParameters();

        Assert.Equal("JWT", parameters.TokenType);
        Assert.True(parameters.AddKeyIdHeader);
        Assert.Null(parameters.SigningCredentials);
        Assert.Null(parameters.EncryptionCredentials);
    }

    #endregion

    #region ValidateJwtParameters Tests

    [Fact]
    public void ValidateJwtParameters_Defaults_AreExpected()
    {
        var parameters = new ValidateJwtParameters();

        Assert.Equal("AuthenticationTypes.Federation", parameters.AuthenticationType);
        Assert.Equal(ClaimsIdentity.DefaultNameClaimType, parameters.NameClaimType);
        Assert.Equal(ClaimsIdentity.DefaultRoleClaimType, parameters.RoleClaimType);
        Assert.Empty(parameters.Validators);
        Assert.Equal(TimeSpan.FromMinutes(5.0), parameters.ClockSkew);
    }

    [Fact]
    public void AddValidator_AppendsAndReturnsSameInstance()
    {
        var parameters = new ValidateJwtParameters();

        var result = parameters.AddValidator((_, _) => ValueTask.CompletedTask);

        Assert.Same(parameters, result);
        Assert.Single(parameters.Validators);
    }

    [Fact]
    public void UseValidationKeys_ReturnsSameInstance()
    {
        var parameters = new ValidateJwtParameters();

        var result = parameters.UseValidationKeys([]);

        Assert.Same(parameters, result);
    }

    #endregion
}
