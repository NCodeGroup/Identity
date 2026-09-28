#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
# Build context is the repository root (so Directory.Build.* and Directory.Packages.props are available).

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore "src/NCode.Identity.OpenId.Playground/NCode.Identity.OpenId.Playground.csproj"
RUN dotnet build "src/NCode.Identity.OpenId.Playground/NCode.Identity.OpenId.Playground.csproj" -c Release --no-restore -o /app/build

FROM build AS publish
RUN dotnet publish "src/NCode.Identity.OpenId.Playground/NCode.Identity.OpenId.Playground.csproj" -c Release --no-restore -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NCode.Identity.OpenId.Playground.dll"]
