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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace NCode.PasswordGenerator;

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
    /// Generates a random password using the rules from the specified <paramref name="optionsConfigurator"/>.
    /// </summary>
    /// <param name="optionsConfigurator">A callback method that is invoked to configure the <see cref="PasswordGeneratorOptions"/>.</param>
    /// <returns>The newly generated random password.</returns>
    string Generate(Action<PasswordGeneratorOptions> optionsConfigurator);

    /// <summary>
    /// Generates a random password using the rules from the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    /// <returns>The newly generated random password.</returns>
    string Generate(PasswordGeneratorOptions options);

    /// <summary>
    /// Generates a random password using the default options.
    /// </summary>
    /// <param name="password">The destination buffer for the newly generated password.</param>
    void Generate(Span<char> password);

    /// <summary>
    /// Generates a random password using the rules from the specified <paramref name="optionsConfigurator"/>.
    /// </summary>
    /// <param name="optionsConfigurator">A callback method that is invoked to configure the <see cref="PasswordGeneratorOptions"/>.</param>
    /// <param name="password">The destination buffer for the newly generated password.</param>
    void Generate(Action<PasswordGeneratorOptions> optionsConfigurator, Span<char> password);

    /// <summary>
    /// Generates a random password using the rules from the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    /// <param name="password">The destination buffer for the newly generated password.</param>
    void Generate(PasswordGeneratorOptions options, Span<char> password);
}

/// <summary>
/// Provides a default implementation of the <see cref="IPasswordGenerator"/> interface.
/// </summary>
public class PasswordGenerator : IPasswordGenerator
{
    /// <summary>
    /// Gets a singleton instance for <see cref="IPasswordGenerator"/>.
    /// </summary>
    public static IPasswordGenerator Default { get; } = new PasswordGenerator();

    private PasswordGeneratorOptions DefaultOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGenerator"/> class with default options.
    /// </summary>
    public PasswordGenerator()
        : this(new PasswordGeneratorOptions())
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGenerator"/> class with the specified options.
    /// </summary>
    /// <param name="options">The <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    public PasswordGenerator(PasswordGeneratorOptions options)
    {
        DefaultOptions = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGenerator"/> class with the specified options.
    /// </summary>
    /// <param name="optionsAccessor">An accessor for the <see cref="PasswordGeneratorOptions"/> that defines the rules for generating passwords.</param>
    public PasswordGenerator(IOptions<PasswordGeneratorOptions> optionsAccessor)
        : this(optionsAccessor.Value)
    {
        // nothing
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual int GetRandomInt32(int toExclusive) =>
        RandomNumberGenerator.GetInt32(toExclusive);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual int GetRandomInt32(int fromInclusive, int toExclusive) =>
        RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);

    private PasswordGeneratorOptions Configure(Action<PasswordGeneratorOptions> optionsConfigurator)
    {
        var options = DefaultOptions.Clone();
        optionsConfigurator(options);
        return options;
    }

    /// <inheritdoc />
    public string Generate() =>
        Generate(DefaultOptions);

    /// <inheritdoc />
    public string Generate(Action<PasswordGeneratorOptions> optionsConfigurator) =>
        Generate(Configure(optionsConfigurator));

    /// <inheritdoc />
    public virtual string Generate(PasswordGeneratorOptions options) =>
        string.Create(GetLength(options), options, (span, localOptions) => Generate(localOptions, span));

    /// <inheritdoc />
    public void Generate(Span<char> password) =>
        Generate(DefaultOptions, password);

    /// <inheritdoc />
    public void Generate(Action<PasswordGeneratorOptions> optionsConfigurator, Span<char> password) =>
        Generate(Configure(optionsConfigurator), password);

    /// <inheritdoc />
    public void Generate(PasswordGeneratorOptions options, Span<char> password)
    {
        if (password.Length < options.MinLength)
            throw new InvalidOperationException("The requested password length is too small.");

        if (password.Length > options.MaxLength)
            throw new InvalidOperationException("The requested password length is too large.");

        var minCharacters = options.MinSpecialCharacters +
                            options.MinNumericCharacters +
                            options.MinLowercaseCharacters +
                            options.MinUppercaseCharacters;

        if (minCharacters == 0)
            throw new InvalidOperationException("At least one character set must have a minimum greater than zero.");

        if (minCharacters > password.Length)
            throw new InvalidOperationException("The provided destination buffer is too small to hold the minimum amount of required characters.");

        var maxAttempts = options.MaxAttempts;
        var characterSet = FisherYatesShuffle(options.CharacterSet);

        for (var attempt = 0; attempt < maxAttempts; ++attempt)
        {
            GenerateCore(options, characterSet, password);
            if (IsValid(options, password)) return;
        }

        throw new InvalidOperationException("Too many attempts.");
    }

    internal virtual string FisherYatesShuffle(string value) =>
        string.Create(value.Length, value, (dst, src) =>
        {
            src.AsSpan().CopyTo(dst);
            FisherYatesShuffle(dst);
        });

    private void FisherYatesShuffle(Span<char> chars)
    {
        for (var i = 0; i < chars.Length - 1; ++i)
        {
            var j = GetRandomInt32(i, chars.Length);
            if (i == j) continue;
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }

    internal int GetLength(PasswordGeneratorOptions options) =>
        options.MinLength == options.MaxLength ?
            options.MinLength :
            GetRandomInt32(options.MinLength, options.MaxLength + 1);

    // for unit tests
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual bool SkipIsValid([NotNullWhen(true)] out bool? result)
    {
        result = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsValid(PasswordGeneratorOptions options, ReadOnlySpan<char> password) =>
        SkipIsValid(out var result) ? result.Value : options.IsValid(password);

    // for unit tests
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual bool SkipGenerateCore([NotNullWhen(true)] out string? result)
    {
        result = null;
        return false;
    }

    private void GenerateCore(PasswordGeneratorOptions options, string characterSet, Span<char> password)
    {
        if (SkipGenerateCore(out var result))
        {
            result.AsSpan().CopyTo(password);
            return;
        }

        var special = options.SpecialCharacters;
        const string numeric = PasswordGeneratorOptions.NumericCharacters;
        const string lowercase = PasswordGeneratorOptions.LowercaseCharacters;
        const string uppercase = PasswordGeneratorOptions.UppercaseCharacters;

        var remainingSpecial = options.MinSpecialCharacters;
        var remainingNumeric = options.MinNumericCharacters;
        var remainingLowercase = options.MinLowercaseCharacters;
        var remainingUppercase = options.MinUppercaseCharacters;

        for (var i = 0; i < password.Length; ++i)
        {
            if (remainingSpecial > 0)
            {
                password[i] = special[GetRandomInt32(special.Length)];
                --remainingSpecial;
            }
            else if (remainingNumeric > 0)
            {
                password[i] = numeric[GetRandomInt32(numeric.Length)];
                --remainingNumeric;
            }
            else if (remainingLowercase > 0)
            {
                password[i] = lowercase[GetRandomInt32(lowercase.Length)];
                --remainingLowercase;
            }
            else if (remainingUppercase > 0)
            {
                password[i] = uppercase[GetRandomInt32(uppercase.Length)];
                --remainingUppercase;
            }
            else
            {
                password[i] = characterSet[GetRandomInt32(characterSet.Length)];
            }
        }

        FisherYatesShuffle(password);
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
