#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NCode.Identity.OpenId.Playground/NCode.Identity.OpenId.Playground.csproj", "NCode.Identity.OpenId.Playground/"]
RUN dotnet restore "NCode.Identity.OpenId.Playground/NCode.Identity.OpenId.Playground.csproj"
COPY . .
WORKDIR "/src/NCode.Identity.OpenId.Playground"
RUN dotnet build "NCode.Identity.OpenId.Playground.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NCode.Identity.OpenId.Playground.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NCode.Identity.OpenId.Playground.dll"]