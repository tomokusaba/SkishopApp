# ── Build Stage ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install protobuf-compiler only
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    protobuf-compiler && \
    rm -rf /var/lib/apt/lists/*

COPY ["Services/PointService/PointService.csproj", "Services/PointService/"]
RUN dotnet restore "Services/PointService/PointService.csproj"

COPY ["Services/", "Services/"]
WORKDIR "/src/Services/PointService"
RUN PROTOBUF_PROTOC=/usr/bin/protoc dotnet publish "PointService.csproj" -c Debug -o /app/publish --self-contained false -p:ProtobufCompiler=system 

# ── Runtime Stage ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_gcServer=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PointService.dll"]
