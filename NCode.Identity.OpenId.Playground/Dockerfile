#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["NIdentity.OpenId.Playground/NIdentity.OpenId.Playground.csproj", "NIdentity.OpenId.Playground/"]
RUN dotnet restore "NIdentity.OpenId.Playground/NIdentity.OpenId.Playground.csproj"
COPY . .
WORKDIR "/src/NIdentity.OpenId.Playground"
RUN dotnet build "NIdentity.OpenId.Playground.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NIdentity.OpenId.Playground.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NIdentity.OpenId.Playground.dll"]