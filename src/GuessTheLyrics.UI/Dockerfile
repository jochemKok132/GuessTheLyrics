# Build from the repository root:
#   docker build -f src/GuessTheLyrics.UI/Dockerfile --build-arg VERSION=1.2.3 -t guessthelyrics-ui .
#
# Blazor WebAssembly is static files after publishing, so the final image is just nginx.
# BASE_PATH (default "/") sets <base href> at container start, e.g. BASE_PATH=/guessthelyrics/.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src

COPY Directory.Build.props ./
COPY src/GuessTheLyrics.UI/GuessTheLyrics.UI.csproj src/GuessTheLyrics.UI/
COPY src/GuessTheLyrics.Shared/GuessTheLyrics.Shared.csproj src/GuessTheLyrics.Shared/
RUN dotnet restore src/GuessTheLyrics.UI/GuessTheLyrics.UI.csproj

COPY src/GuessTheLyrics.UI/ src/GuessTheLyrics.UI/
COPY src/GuessTheLyrics.Shared/ src/GuessTheLyrics.Shared/
RUN dotnet publish src/GuessTheLyrics.UI/GuessTheLyrics.UI.csproj \
    -c Release -o /publish --no-restore -p:Version=${VERSION}

FROM nginx:1.27-alpine AS final
ENV BASE_PATH=/
COPY src/GuessTheLyrics.UI/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/GuessTheLyrics.UI/docker/40-base-href.sh /docker-entrypoint.d/40-base-href.sh
RUN chmod +x /docker-entrypoint.d/40-base-href.sh
COPY --from=build /publish/wwwroot /usr/share/nginx/html
EXPOSE 8080
