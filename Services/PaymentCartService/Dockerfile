# Stage 1: ビルド
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PaymentCartService/PaymentCartService.csproj", "PaymentCartService/"]
RUN dotnet restore "PaymentCartService/PaymentCartService.csproj"

COPY ["PaymentCartService/", "PaymentCartService/"]
WORKDIR "/src/PaymentCartService"
RUN dotnet publish "PaymentCartService.csproj" -c Release -o /app/publish --no-restore

# Stage 2: ランタイム
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 非 root ユーザーの作成（必須）
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_gcServer=1

EXPOSE 5005

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5005/health || exit 1

ENTRYPOINT ["dotnet", "PaymentCartService.dll"]
