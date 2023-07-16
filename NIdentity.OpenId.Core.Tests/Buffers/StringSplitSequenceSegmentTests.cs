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
using NIdentity.OpenId.Buffers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Buffers;

public class StringSplitSequenceSegmentTests
{
    [Theory]
    [InlineData("", '.', 1)]
    [InlineData(".", '.', 2)]
    [InlineData("01", '.', 1)]
    [InlineData("01.34", '.', 2)]
    [InlineData("01.34.", '.', 3)]
    [InlineData(".01.34.", '.', 4)]
    [InlineData("01.34.67", '.', 3)]
    [InlineData(".01.34.67.", '.', 5)]
    public void Split_Valid(string value, char separator, int expectedCount)
    {
        var counter = 0;
        var builder = new StringBuilder();
        var segment = StringSplitSequenceSegment.Split(value, separator, out var count);
        do
        {
            if (counter++ > 0) builder.Append(separator);
            builder.Append(segment.Memory);
            segment = segment.Next;
        } while (segment != null);

        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedCount, counter);
        Assert.Equal(value, builder.ToString());
    }
}
