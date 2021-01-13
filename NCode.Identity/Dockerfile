#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["NCode.Identity/NCode.Identity.csproj", "NCode.Identity/"]
RUN dotnet restore "NCode.Identity/NCode.Identity.csproj"
COPY . .
WORKDIR "/src/NCode.Identity"
RUN dotnet build "NCode.Identity.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NCode.Identity.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NCode.Identity.dll"]