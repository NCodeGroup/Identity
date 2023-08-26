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

using Microsoft.Extensions.Options;

namespace NCode.Jose.Tests;

public class AlgorithmFilterTests : BaseTests
{
    [Fact]
    public void Exclude_Valid()
    {
        const string disabledCode = nameof(disabledCode);
        const string enabledCode = nameof(enabledCode);

        var options = new JoseOptions
        {
            DisabledAlgorithms = new List<string>
            {
                disabledCode
            }
        };

        var optionsAccessor = Options.Create(options);
        var filter = new AlgorithmFilter(optionsAccessor);

        var mockAlgorithm = CreateStrictMock<IAlgorithm>();

        mockAlgorithm
            .Setup(_ => _.Code)
            .Returns(disabledCode)
            .Verifiable();

        var result = filter.Exclude(mockAlgorithm.Object);
        Assert.True(result);

        mockAlgorithm
            .Setup(_ => _.Code)
            .Returns(enabledCode)
            .Verifiable();

        result = filter.Exclude(mockAlgorithm.Object);
        Assert.False(result);
    }
}
