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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Options;

public class DynamicByHostOpenIdTenantOptions
{
    /// <summary>
    /// Gets or sets the regex pattern to extract the domain name.
    /// The default pattern uses the entire value.
    /// </summary>
    public string RegexPattern { get; set; } = ".*";

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// When specified, this value must include a leading slash.
    /// The default value is undefined (aka empty).
    /// </summary>
    public PathString TenantPath { get; set; }
}
