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

using Moq;

namespace NCode.Identity.OpenId.Tests;

public class BaseTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        MockRepository.Verify();
    }

    protected Mock<T> CreateStrictMock<T>(params object[] args)
        where T : class =>
        MockRepository.Create<T>(args);

    protected Mock<T> CreateLooseMock<T>(params object[] args)
        where T : class =>
        MockRepository.Create<T>(MockBehavior.Loose, args);

    protected Mock<T> CreatePartialMock<T>(params object[] args)
        where T : class
    {
        var mock = CreateLooseMock<T>(args);
        mock.CallBase = true;
        return mock;
    }
}
