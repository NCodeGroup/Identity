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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

public class DateTimeConverterTests
{
    private readonly DateTimeConverter _converter = new();

    #region ConvertToProvider Tests

    [Fact]
    public void ConvertToProvider_ConvertsToUtc()
    {
        var local = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);

        var result = Assert.IsType<DateTime>(_converter.ConvertToProvider(local));

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(local.ToUniversalTime(), result);
    }

    #endregion

    #region ConvertFromProvider Tests

    [Fact]
    public void ConvertFromProvider_ConvertsToUtc()
    {
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = Assert.IsType<DateTime>(_converter.ConvertFromProvider(utc));

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utc, result);
    }

    #endregion
}
