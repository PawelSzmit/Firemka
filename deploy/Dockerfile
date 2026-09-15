FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Firemka.Application/Firemka.Application.csproj src/Firemka.Application/
COPY src/Firemka.Application/packages.lock.json src/Firemka.Application/
COPY src/Firemka.Domain/Firemka.Domain.csproj src/Firemka.Domain/
COPY src/Firemka.Domain/packages.lock.json src/Firemka.Domain/
COPY src/Firemka.Infrastructure/Firemka.Infrastructure.csproj src/Firemka.Infrastructure/
COPY src/Firemka.Infrastructure/packages.lock.json src/Firemka.Infrastructure/
COPY src/Firemka.Web/Firemka.Web.csproj src/Firemka.Web/
COPY src/Firemka.Web/packages.lock.json src/Firemka.Web/
COPY src/Firemka.Worker/Firemka.Worker.csproj src/Firemka.Worker/
COPY src/Firemka.Worker/packages.lock.json src/Firemka.Worker/
COPY docs/research/contracts/ docs/research/contracts/
RUN dotnet restore src/Firemka.Web/Firemka.Web.csproj --locked-mode
RUN dotnet restore src/Firemka.Worker/Firemka.Worker.csproj --locked-mode

COPY src/ src/

FROM build AS publish-web
RUN dotnet publish src/Firemka.Web/Firemka.Web.csproj --no-restore --configuration Release --output /app/web /p:UseAppHost=false

FROM build AS publish-worker
RUN dotnet publish src/Firemka.Worker/Firemka.Worker.csproj --no-restore --configuration Release --output /app/worker /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS runtime-base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl fonts-liberation2 jq \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl --fail --silent --show-error \
        --output /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
        https://www.postgresql.org/media/keys/ACCC4CF8.asc \
    && . /etc/os-release \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" \
        > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install --yes --no-install-recommends postgresql-client-18 \
    && groupadd --gid 10001 firemka \
    && useradd --uid 10001 --gid firemka --no-create-home --shell /usr/sbin/nologin firemka \
    && mkdir --parents /var/lib/firemka/keys /var/lib/firemka/files \
    && chown --recursive firemka:firemka /var/lib/firemka \
    && rm --recursive --force /var/lib/apt/lists/*
WORKDIR /app
USER firemka

FROM runtime-base AS web
COPY --from=publish-web --chown=firemka:firemka /app/web ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Firemka.Web.dll"]

FROM runtime-base AS worker
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends poppler-utils tesseract-ocr tesseract-ocr-eng tesseract-ocr-pol \
    && rm --recursive --force /var/lib/apt/lists/*
USER firemka
COPY --from=publish-worker --chown=firemka:firemka /app/worker ./
ENTRYPOINT ["dotnet", "Firemka.Worker.dll"]
