FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Migrador/Migrador.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
COPY db/scripts/ db/scripts/
USER app
ENTRYPOINT ["dotnet", "Migrador.dll"]