FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SalesManagementService/SalesManagementService.csproj", "SalesManagementService/"]
RUN dotnet restore "SalesManagementService/SalesManagementService.csproj"
COPY . .
WORKDIR "/src/SalesManagementService"
RUN dotnet publish "SalesManagementService.csproj" -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 5004
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5004/health || exit 1
ENTRYPOINT ["dotnet", "SalesManagementService.dll"]
