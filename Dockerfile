FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source

COPY global.json ./
COPY src/JobMatch.Api/JobMatch.Api.csproj src/JobMatch.Api/
COPY src/JobMatch.Application/JobMatch.Application.csproj src/JobMatch.Application/
COPY src/JobMatch.Domain/JobMatch.Domain.csproj src/JobMatch.Domain/
COPY src/JobMatch.Infrastructure/JobMatch.Infrastructure.csproj src/JobMatch.Infrastructure/
RUN dotnet restore src/JobMatch.Api/JobMatch.Api.csproj

COPY src/ src/
RUN dotnet publish src/JobMatch.Api/JobMatch.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish/ ./
COPY deploy/start-demo.sh ./start-demo.sh
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "JobMatch.Api.dll"]
