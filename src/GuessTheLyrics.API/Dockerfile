# Build from the repository root:
#   docker build -f src/GuessTheLyrics.API/Dockerfile --build-arg VERSION=1.2.3 -t guessthelyrics-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src

# Project files first, so the restore layer stays cached until a .csproj changes.
COPY Directory.Build.props ./
COPY src/GuessTheLyrics.API/GuessTheLyrics.API.csproj src/GuessTheLyrics.API/
COPY src/GuessTheLyrics.Application/GuessTheLyrics.Application.csproj src/GuessTheLyrics.Application/
COPY src/GuessTheLyrics.Domain/GuessTheLyrics.Domain.csproj src/GuessTheLyrics.Domain/
COPY src/GuessTheLyrics.Infrastructure/GuessTheLyrics.Infrastructure.csproj src/GuessTheLyrics.Infrastructure/
COPY src/GuessTheLyrics.Shared/GuessTheLyrics.Shared.csproj src/GuessTheLyrics.Shared/
RUN dotnet restore src/GuessTheLyrics.API/GuessTheLyrics.API.csproj

COPY src/ src/
RUN dotnet publish src/GuessTheLyrics.API/GuessTheLyrics.API.csproj \
    -c Release -o /app --no-restore -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
# The aspnet image runs as the non-root "app" user by default.
ENTRYPOINT ["dotnet", "GuessTheLyrics.API.dll"]
