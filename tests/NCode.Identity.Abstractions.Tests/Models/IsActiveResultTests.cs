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

namespace NCode.Identity.Models;

public class IsActiveResultTests
{
    #region PositiveIsActiveResult Tests

    [Fact]
    public void PositiveIsActiveResult_WhenConstructed_IsActive()
    {
        var result = new PositiveIsActiveResult();

        Assert.True(result.IsActive);
    }

    [Fact]
    public void PositiveIsActiveResult_WhenSetInactive_IsNotActive()
    {
        var result = new PositiveIsActiveResult();

        result.SetInactive();

        Assert.False(result.IsActive);
    }

    #endregion

    #region NegativeIsActiveResult Tests

    [Fact]
    public void NegativeIsActiveResult_WhenConstructed_IsNotActive()
    {
        var result = new NegativeIsActiveResult();

        Assert.False(result.IsActive);
    }

    [Fact]
    public void NegativeIsActiveResult_WhenSetActive_IsActive()
    {
        var result = new NegativeIsActiveResult();

        result.SetActive();

        Assert.True(result.IsActive);
    }

    #endregion
}
