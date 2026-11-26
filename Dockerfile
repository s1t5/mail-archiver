FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY . ./
RUN dotnet restore MailArchiver.csproj && dotnet publish MailArchiver.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install Kerberos library required by MailKit (libgssapi_krb5.so.2) and curl for the
# liveness health check below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/out ./

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENV TZ=UTC

# Liveness only: the check answers without authentication, so it works on a fresh install
# and with the REST API disabled, and it does not touch PostgreSQL, so a database outage
# does not make Docker restart a healthy process. The root path redirects to the login
# page before any controller runs. No configuration is required.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD sh -c 'curl -fsS -o /dev/null --max-time 4 http://localhost:5000/ || exit 1'

ENTRYPOINT ["dotnet", "MailArchiver.dll"]
