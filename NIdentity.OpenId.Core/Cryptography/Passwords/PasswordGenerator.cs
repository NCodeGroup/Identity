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

using System.Collections;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace NIdentity.OpenId.Cryptography.Passwords;

/// <summary>
/// Provides the ability to generate random passwords.
/// </summary>
/// <remarks>
/// Be aware that the <see cref="IEnumerable{T}"/> implementation is an infinite sequence.
/// </remarks>
public interface IPasswordGenerator : IEnumerable<string>
{
    /// <summary>
    /// Generates a random password using the default options.
    /// </summary>
    /// <returns>The newly generated random password.</returns>
    string Generate();

    /// <summary>
    /// Generates a random password using the rules from the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    /// <returns>The newly generated random password.</returns>
    string Generate(PasswordGeneratorOptions options);
}

/// <summary>
/// Provides the default implementation for <see cref="IPasswordGenerator"/>.
/// </summary>
public class PasswordGenerator : IPasswordGenerator
{
    private PasswordGeneratorOptions DefaultOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGenerator"/> class.
    /// </summary>
    /// <param name="options">The <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    public PasswordGenerator(PasswordGeneratorOptions options)
    {
        DefaultOptions = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGenerator"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An accessor for the <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    public PasswordGenerator(IOptions<PasswordGeneratorOptions> optionsAccessor)
        : this(optionsAccessor.Value)
    {
        // nothing
    }

    internal virtual int GetRandomInt32(int toExclusive) =>
        RandomNumberGenerator.GetInt32(toExclusive);

    internal virtual int GetRandomInt32(int fromInclusive, int toExclusive) =>
        RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);

    /// <inheritdoc />
    public virtual string Generate() =>
        Generate(DefaultOptions);

    /// <inheritdoc />
    public virtual string Generate(PasswordGeneratorOptions options)
    {
        var maxAttempts = options.MaxAttempts;
        var characterSetOriginal = options.CharacterSet;
        var characterSetLength = characterSetOriginal.Length;
        var characterSetShuffled = new string(characterSetOriginal.OrderBy(_ => GetRandomInt32(characterSetLength)).ToArray());

        for (var attempt = 0; attempt < maxAttempts; ++attempt)
        {
            var password = GenerateCore(options, characterSetShuffled);
            if (IsValid(options, password))
            {
                return password;
            }
        }

        throw new InvalidOperationException("Too many attempts.");
    }

    internal virtual int GetLength(PasswordGeneratorOptions options) =>
        options.MinLength == options.MaxLength ?
            options.MinLength :
            GetRandomInt32(options.MinLength, options.MaxLength + 1);

    internal virtual bool IsValid(PasswordGeneratorOptions options, string password) =>
        options.IsValid(password);

    internal virtual string GenerateCore(PasswordGeneratorOptions options, string characterSet)
    {
        var length = GetLength(options);
        var password = new char[length];

        for (var i = 0; i < length; ++i)
        {
            password[i] = characterSet[GetRandomInt32(characterSet.Length)];

            if (options.AreIdentical(i, password))
            {
                --i;
            }
        }

        return new string(password);
    }

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator()
    {
        var count = DefaultOptions.MaxEnumerations;
        while (--count >= 0) yield return Generate();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
