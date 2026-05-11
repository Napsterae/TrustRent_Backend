FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TrustRent.Gateway/TrustRent.Gateway.csproj TrustRent.Gateway/
COPY TrustRent.Shared/TrustRent.Shared.csproj TrustRent.Shared/

RUN dotnet restore TrustRent.Gateway/TrustRent.Gateway.csproj

COPY . .
RUN dotnet publish TrustRent.Gateway/TrustRent.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ENTRYPOINT ["dotnet", "TrustRent.Gateway.dll"]