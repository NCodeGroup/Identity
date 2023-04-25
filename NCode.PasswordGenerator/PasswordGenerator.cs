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
            for (var i = 0; i < dst.Length - 1; ++i)
            {
                var j = GetRandomInt32(i, dst.Length);
                if (i == j) continue;
                (dst[i], dst[j]) = (dst[j], dst[i]);
            }
        });

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

        for (var i = 0; i < password.Length; ++i)
        {
            password[i] = characterSet[GetRandomInt32(characterSet.Length)];

            if (options.AreIdentical(i, password))
            {
                --i;
            }
        }
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
