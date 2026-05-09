FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TrustRent.sln ./
COPY TrustRent.Api/TrustRent.Api.csproj TrustRent.Api/
COPY TrustRent.Modules.Admin/TrustRent.Modules.Admin.csproj TrustRent.Modules.Admin/
COPY TrustRent.Modules.Catalog/TrustRent.Modules.Catalog.csproj TrustRent.Modules.Catalog/
COPY TrustRent.Modules.Communications/TrustRent.Modules.Communications.csproj TrustRent.Modules.Communications/
COPY TrustRent.Modules.Identity/TrustRent.Modules.Identity.csproj TrustRent.Modules.Identity/
COPY TrustRent.Modules.Leasing/TrustRent.Modules.Leasing.csproj TrustRent.Modules.Leasing/
COPY TrustRent.Shared/TrustRent.Shared.csproj TrustRent.Shared/

RUN dotnet restore TrustRent.Api/TrustRent.Api.csproj

COPY . .
RUN dotnet publish TrustRent.Api/TrustRent.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ENTRYPOINT ["dotnet", "TrustRent.Api.dll"]