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

namespace NIdentity.OpenId.Playground.Extensions;

internal static class HttpResponseExtensions
{
    public static void SetNoCache(this HttpResponse response)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        response.Headers["Pragma"] = "no-cache";
        response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
    }
}
