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

namespace NIdentity.OpenId.Settings;

public static class SettingMergeBehaviors
{
    public static class Boolean
    {
        public const string And = "and";
        public const string Or = "or";
    }

    public static class Scalar
    {
        public const string Replace = "replace";
        public const string Append = "append";
    }

    public static class Set
    {
        public const string Intersect = "intersect";
        public const string Union = "union";
        public const string Except = "except";
    }
}
