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

using System.Text;

namespace NIdentity.OpenId.Cryptography.Passwords;

/// <summary>
/// Contains the various rules and options for password generation.
/// </summary>
public class PasswordGeneratorOptions
{
    private const int DefaultMaxEnumerations = 100;
    private const int DefaultMaxAttempts = 10000;
    private const int DefaultMinLength = 16;
    private const int DefaultMaxLength = 64;
    private const int DefaultMaxConsecutiveIdenticalCharacters = 2;

    private const string LowercaseCharacters = "abcdefghijklmnopqrstuvwxyz";
    private const string UppercaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string NumericCharacters = "0123456789";
    private const string DefaultSpecialCharacters = @"!#$%&*@\";

    private int _minLength = DefaultMinLength;
    private int _maxLength = DefaultMaxLength;

    /// <summary>
    /// Gets or sets the maximum number of passwords to generate when using <see cref="IEnumerable{T}"/>.
    /// </summary>
    public int MaxEnumerations { get; set; } = DefaultMaxEnumerations;

    /// <summary>
    /// Gets or set the maximum number of iterations to perform when attempting to generate a valid password.
    /// </summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>
    /// Gets or sets the minimum length for generated passwords.
    /// </summary>
    public int MinLength
    {
        get => _minLength;
        set
        {
            if (value > MaxLength)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MinLength cannot be greater than MaxLength.");

            _minLength = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum length for generated passwords.
    /// </summary>
    public int MaxLength
    {
        get => _maxLength;
        set
        {
            if (value < MinLength)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxLength cannot be less than MinLength.");

            _maxLength = value;
        }
    }

    /// <summary>
    /// Sets the exact length for generated passwords.
    /// </summary>
    public int ExactLength
    {
        set => SetLengthRange(value, value);
    }

    /// <summary>
    /// Sets the <see cref="MinLength"/> and <see cref="MaxLength"/> properties at the same time.
    /// </summary>
    /// <param name="minLength">The minimum length for generated passwords.</param>
    /// <param name="maxLength">The maximum length for generated passwords.</param>
    public void SetLengthRange(int minLength, int maxLength)
    {
        if (minLength > maxLength)
            throw new InvalidOperationException();

        _minLength = minLength;
        _maxLength = maxLength;
    }

    /// <summary>
    /// Gets or sets the maximum number of consecutive identical characters allowed in the generated password.
    /// </summary>
    public int MaxConsecutiveIdenticalCharacters { get; set; } = DefaultMaxConsecutiveIdenticalCharacters;

    /// <summary>
    /// Gets or sets a boolean indicating whether lower-case characters are to be included in the generated password.
    /// </summary>
    public bool IncludeLowercase { get; set; } = true;

    /// <summary>
    /// Gets or sets a boolean indicating whether upper-case characters are to be included in the generated password.
    /// </summary>
    public bool IncludeUppercase { get; set; } = true;

    /// <summary>
    /// Gets or ses a boolean indicating whether numbers are to be included in the generated password.
    /// </summary>
    public bool IncludeNumeric { get; set; } = true;

    /// <summary>
    /// Gets or sets a boolean indicating whether special characters such as punctuations and symbols are to be included in the generated password.
    /// </summary>
    public bool IncludeSpecial { get; set; } = true;

    private string? SpecialOrNull { get; set; }

    /// <summary>
    /// Gets or sets the list of special characters such as punctuations and symbols that are allowed to be in a password.
    /// </summary>
    public string SpecialCharacters
    {
        get => SpecialOrNull ?? DefaultSpecialCharacters;
        set => SpecialOrNull = value;
    }

    /// <summary>
    /// Gets a <see cref="string"/> containing the allowable characters in a password.
    /// </summary>
    public string CharacterSet
    {
        get
        {
            var builder = new StringBuilder();
            if (IncludeLowercase) builder.Append(LowercaseCharacters);
            if (IncludeUppercase) builder.Append(UppercaseCharacters);
            if (IncludeNumeric) builder.Append(NumericCharacters);
            if (IncludeSpecial) builder.Append(SpecialCharacters);
            return builder.ToString();
        }
    }

    internal bool AreIdentical(int i, ReadOnlySpan<char> password)
    {
        var maxConsecutive = MaxConsecutiveIdenticalCharacters;
        return i >= maxConsecutive && AreIdentical(password.Slice(i - maxConsecutive, maxConsecutive + 1));
    }

    private static bool AreIdentical(ReadOnlySpan<char> slice)
    {
        var prev = slice[0];

        foreach (var c in slice[1..])
        {
            if (c != prev) return false;
            prev = c;
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> or <c>false</c> whether the specified <paramref name="password"/> is valid according to configured
    /// rules on the current <see cref="PasswordGeneratorOptions"/> instance.
    /// </summary>
    /// <param name="password">The password to check.</param>
    /// <returns><c>true</c> is the password is valid; otherwise, <c>false</c>.</returns>
    public bool IsValid(ReadOnlySpan<char> password)
    {
        if (password.Length < MinLength) return false;
        if (password.Length > MaxLength) return false;

        var hasLowercase = false;
        var hasUppercase = false;
        var hasNumeric = false;
        var hasSpecial = false;

        for (var i = 0; i < password.Length; ++i)
        {
            var c = password[i];

            hasLowercase = hasLowercase || LowercaseCharacters.Contains(c);
            hasUppercase = hasUppercase || UppercaseCharacters.Contains(c);
            hasNumeric = hasNumeric || NumericCharacters.Contains(c);
            hasSpecial = hasSpecial || SpecialCharacters.Contains(c);

            if (AreIdentical(i, password))
            {
                return false;
            }
        }

        return (!IncludeLowercase || hasLowercase) &&
               (!IncludeUppercase || hasUppercase) &&
               (!IncludeNumeric || hasNumeric) &&
               (!IncludeSpecial || hasSpecial);
    }
}
