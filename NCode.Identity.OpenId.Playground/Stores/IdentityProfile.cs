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

using AutoMapper;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Playground.DataLayer.Entities;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Playground.Stores;

internal class IdentityProfile : Profile
{
    public IdentityProfile()
    {
        CreateMap<ClientUrlEntity, Uri>()
            .ConstructUsing(src => new Uri(src.Url))
            .ReverseMap();

        CreateMap<SecretEntity, PersistedSecret>()
            .ReverseMap();

        CreateMap<ClientEntity, PersistedClient>()
            .ForMember(dst => dst.Secrets, cfg => cfg.MapFrom(src => src.ClientSecrets.Select(entity => entity.Secret)))
            .ForMember(dst => dst.RedirectUrls, cfg => cfg.MapFrom(src => src.Urls.Where(clientUrl => clientUrl.UrlType == "RedirectUrl")))
            .ReverseMap();
    }
}
