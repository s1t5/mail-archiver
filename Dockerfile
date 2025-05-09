# Verwende das offizielle .NET SDK Image als Build-Umgebung
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Setze das Arbeitsverzeichnis innerhalb des Containers
WORKDIR /app

# Kopiere die csproj und restore Dependencies
COPY *.csproj ./
RUN dotnet restore

# Kopiere den Rest des Projektcodes
COPY . ./

# Baue die Anwendung
RUN dotnet publish -c Release -o out

# Verwende das offizielle .NET Runtime Image als Runtime-Umgebung
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Setze das Arbeitsverzeichnis für das Runtime-Image
WORKDIR /app

# Kopiere die gebaute Anwendung aus dem Build-Stage
COPY --from=build /app/out ./

# Setze Umgebungsvariablen
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Exponiere den Port, den die Anwendung verwendet
EXPOSE 5000

ENV TZ=UTC

# Starte die Anwendung
ENTRYPOINT ["dotnet", "MailArchiver.dll"]
