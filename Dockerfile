# syntax=docker/dockerfile:1

# --- Build-fase -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Eerst alleen de .csproj-bestanden kopiëren, zodat 'dotnet restore' in de
# Docker-buildcache blijft zitten zolang de package-referenties niet wijzigen
# (pas als de broncode zelf verandert, moet alleen de publish-stap opnieuw).
COPY FileSync.Shared/FileSync.Shared.csproj FileSync.Shared/
COPY FileSync.Server/FileSync.Server.csproj FileSync.Server/
RUN dotnet restore FileSync.Server/FileSync.Server.csproj

COPY FileSync.Shared/ FileSync.Shared/
COPY FileSync.Server/ FileSync.Server/
RUN dotnet publish FileSync.Server/FileSync.Server.csproj -c Release -o /app --no-restore

# --- Runtime-fase -----------------------------------------------------------
# Alleen de kale .NET-runtime nodig (geen ASP.NET Core): dit is een console-app.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Opslagmap als volume, zodat gesynchroniseerde bestanden de levenscyclus van de
# container overleven (koppel dit aan een host-map of named volume bij 'docker run').
VOLUME ["/data"]

# 4711 = kale TCP (verplicht), 4712 = optionele TLS-variant (PROTOCOL.md §8).
EXPOSE 4711 4712

ENTRYPOINT ["dotnet", "FileSync.Server.dll"]
CMD ["--port", "4711", "--storage", "/data"]
