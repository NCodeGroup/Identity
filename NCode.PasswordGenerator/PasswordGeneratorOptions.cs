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

namespace NCode.PasswordGenerator;

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

    private const int DefaultMinSpecialCharacters = 1;
    private const int DefaultMinNumericCharacters = 1;
    private const int DefaultMinLowercaseCharacters = 1;
    private const int DefaultMinUppercaseCharacters = 1;

    internal const string LowercaseCharacters = "abcdefghijklmnopqrstuvwxyz";
    internal const string UppercaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    internal const string NumericCharacters = "0123456789";
    private const string DefaultSpecialCharacters = "!;#$%&()*+,-./:;<=>?@[]^_`{|}~";

    private int _minLength = DefaultMinLength;
    private int _maxLength = DefaultMaxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordGeneratorOptions"/> class.
    /// </summary>
    public PasswordGeneratorOptions()
    {
        Reset();
    }

    private PasswordGeneratorOptions(PasswordGeneratorOptions other)
    {
        _minLength = other._minLength;
        _maxLength = other._maxLength;
        MaxEnumerations = other.MaxEnumerations;
        MaxAttempts = other.MaxAttempts;
        MaxConsecutiveIdenticalCharacters = other.MaxConsecutiveIdenticalCharacters;
        MinLowercaseCharacters = other.MinLowercaseCharacters;
        MinUppercaseCharacters = other.MinUppercaseCharacters;
        MinNumericCharacters = other.MinNumericCharacters;
        MinSpecialCharacters = other.MinSpecialCharacters;
        SpecialOrNull = other.SpecialOrNull;
    }

    /// <summary>
    /// Resets all the properties to their default values.
    /// </summary>
    public PasswordGeneratorOptions Reset()
    {
        _minLength = DefaultMinLength;
        _maxLength = DefaultMaxLength;
        MaxEnumerations = DefaultMaxEnumerations;
        MaxAttempts = DefaultMaxAttempts;
        MaxConsecutiveIdenticalCharacters = DefaultMaxConsecutiveIdenticalCharacters;
        MinLowercaseCharacters = DefaultMinLowercaseCharacters;
        MinUppercaseCharacters = DefaultMinUppercaseCharacters;
        MinNumericCharacters = DefaultMinNumericCharacters;
        MinSpecialCharacters = DefaultMinSpecialCharacters;
        SpecialOrNull = null;
        return this;
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns>A new object that is a copy of this instance.</returns>
    public PasswordGeneratorOptions Clone() => new(this);

    /// <summary>
    /// Gets or sets the maximum number of passwords to generate when using <see cref="IEnumerable{T}"/>.
    /// </summary>
    public int MaxEnumerations { get; set; }

    /// <summary>
    /// Sets the maximum number of passwords to generate when using <see cref="IEnumerable{T}"/>.
    /// </summary>
    public PasswordGeneratorOptions SetMaxEnumerations(int value)
    {
        MaxEnumerations = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the maximum number of iterations to perform when attempting to generate a valid password.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Sets the maximum number of iterations to perform when attempting to generate a valid password.
    /// </summary>
    public PasswordGeneratorOptions SetMaxAttempts(int value)
    {
        MaxAttempts = value;
        return this;
    }

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
    /// Sets the minimum length for generated passwords.
    /// </summary>
    public PasswordGeneratorOptions SetMinLength(int value)
    {
        MinLength = value;
        return this;
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
    /// Sets the maximum length for generated passwords.
    /// </summary>
    public PasswordGeneratorOptions SetMaxLength(int value)
    {
        MaxLength = value;
        return this;
    }

    /// <summary>
    /// Sets the exact length for generated passwords.
    /// </summary>
    public int ExactLength
    {
        set => SetLengthRange(value, value);
    }

    /// <summary>
    /// Sets the exact length for generated passwords.
    /// </summary>
    public PasswordGeneratorOptions SetExactLength(int value)
    {
        ExactLength = value;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="MinLength"/> and <see cref="MaxLength"/> properties at the same time.
    /// </summary>
    /// <param name="minLength">The minimum length for generated passwords.</param>
    /// <param name="maxLength">The maximum length for generated passwords.</param>
    public PasswordGeneratorOptions SetLengthRange(int minLength, int maxLength)
    {
        if (minLength > maxLength)
            throw new InvalidOperationException();

        _minLength = minLength;
        _maxLength = maxLength;

        return this;
    }

    /// <summary>
    /// Gets or sets the maximum number of consecutive identical characters allowed in the generated password.
    /// </summary>
    public int MaxConsecutiveIdenticalCharacters { get; set; }

    /// <summary>
    /// Sets the maximum number of consecutive identical characters allowed in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetMaxConsecutiveIdenticalCharacters(int value)
    {
        MaxConsecutiveIdenticalCharacters = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the minimum amount of lower-case characters that are to be included in the generated password.
    /// </summary>
    public int MinLowercaseCharacters { get; set; }

    /// <summary>
    /// Sets the minimum amount of lower-case characters that are to be included in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetMinLowercaseCharacters(int value)
    {
        MinLowercaseCharacters = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the minimum amount of upper-case characters that are to be included in the generated password.
    /// </summary>
    public int MinUppercaseCharacters { get; set; }

    /// <summary>
    /// Sets the minimum amount of upper-case characters that are to be included in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetMinUppercaseCharacters(int value)
    {
        MinUppercaseCharacters = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the minimum amount of numeric characters that are to be included in the generated password.
    /// </summary>
    public int MinNumericCharacters { get; set; }

    /// <summary>
    /// Sets the minimum amount of numeric characters that are to be included in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetMinNumericCharacters(int value)
    {
        MinNumericCharacters = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the minimum amount of special characters that are to be included in the generated password.
    /// </summary>
    public int MinSpecialCharacters { get; set; }

    /// <summary>
    /// Sets the minimum amount of special characters that are to be included in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetMinSpecialCharacters(int value)
    {
        MinSpecialCharacters = value;
        return this;
    }

    private string? SpecialOrNull { get; set; }

    /// <summary>
    /// Gets or sets the list of special characters such as punctuations and symbols that are to be included in the generated password.
    /// </summary>
    public string SpecialCharacters
    {
        get => SpecialOrNull ?? DefaultSpecialCharacters;
        set => SpecialOrNull = value;
    }

    /// <summary>
    /// Sets the list of special characters such as punctuations and symbols that are to be included in the generated password.
    /// </summary>
    public PasswordGeneratorOptions SetSpecialCharacters(string value)
    {
        SpecialCharacters = value;
        return this;
    }

    /// <summary>
    /// Gets a <see cref="string"/> containing the allowable characters in a password.
    /// </summary>
    public string CharacterSet
    {
        get
        {
            var builder = new StringBuilder();
            if (MinLowercaseCharacters > 0) builder.Append(LowercaseCharacters);
            if (MinUppercaseCharacters > 0) builder.Append(UppercaseCharacters);
            if (MinNumericCharacters > 0) builder.Append(NumericCharacters);
            if (MinSpecialCharacters > 0) builder.Append(SpecialCharacters);
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

        var countSpecial = 0;
        var countNumeric = 0;
        var countLowercase = 0;
        var countUppercase = 0;

        for (var i = 0; i < password.Length; ++i)
        {
            if (AreIdentical(i, password))
            {
                return false;
            }

            var c = password[i];

            // FYI, special may overlap with numeric and alpha
            if (SpecialCharacters.Contains(c))
            {
                ++countSpecial;
            }

            if (char.IsNumber(c))
            {
                ++countNumeric;
            }
            else if (char.IsLower(c))
            {
                ++countLowercase;
            }
            else if (char.IsUpper(c))
            {
                ++countUppercase;
            }
        }

        return countSpecial >= MinSpecialCharacters &&
               countNumeric >= MinNumericCharacters &&
               countLowercase >= MinLowercaseCharacters &&
               countUppercase >= MinUppercaseCharacters;
    }
}
