using System.Security.Cryptography;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

const int PostgresPort = 5432;
const int RedisPort = 6379;
const int KafkaPort = 9092;
const int KafkaUiPort = 8090;
const int MailHogUiPort = 8025;
const int MailHogSmtpPort = 1025;

const int AuthPort = 5001;
const int UserManagementPort = 5002;
const int InventoryPort = 5003;
const int InventoryGrpcPort = 15003;
const int SalesPort = 5004;
const int PaymentCartPort = 5005;
const int PaymentCartGrpcPort = 15005;
const int CouponPort = 5006;
const int PointPort = 5007;
const int PointGrpcPort = 15007;
const int MailSendPort = 5008;
const int AiSupportPort = 5009;
const int McpServerPort = 5010;
const int ApiGatewayPort = 8080;
const int FrontendPort = 3000;
const int AdminPortalPort = 8081;

const string JwtIssuer = "https://skishop.local";
const string JwtAudience = "skishop-api";
var frontendBaseUrl = $"http://localhost:{FrontendPort}";
var frontendSuccessUrl = $"{frontendBaseUrl}/checkout/success";
var frontendCancelUrl = $"{frontendBaseUrl}/checkout/cancel";
var adminPortalBaseUrl = $"http://localhost:{AdminPortalPort}";
var apiGatewayBaseUrl = $"http://localhost:{ApiGatewayPort}";
var authBaseUrl = $"http://localhost:{AuthPort}";
var userManagementBaseUrl = $"http://localhost:{UserManagementPort}";
var inventoryBaseUrl = $"http://localhost:{InventoryPort}";
var inventoryGrpcBaseUrl = $"http://localhost:{InventoryGrpcPort}";
var salesBaseUrl = $"http://localhost:{SalesPort}";
var paymentCartBaseUrl = $"http://localhost:{PaymentCartPort}";
var paymentCartGrpcBaseUrl = $"http://localhost:{PaymentCartGrpcPort}";
var couponBaseUrl = $"http://localhost:{CouponPort}";
var pointBaseUrl = $"http://localhost:{PointPort}";
var pointGrpcBaseUrl = $"http://localhost:{PointGrpcPort}";
var mailSendBaseUrl = $"http://localhost:{MailSendPort}";
var aiSupportBaseUrl = $"http://localhost:{AiSupportPort}";
var mcpServerBaseUrl = $"http://localhost:{McpServerPort}";

string GetConfiguredOrDefault(string key, string fallback)
{
    var configuredValue = builder.Configuration[key];
    return string.IsNullOrWhiteSpace(configuredValue) ? fallback : configuredValue;
}

string GetConfiguredOrGenerated(string key, Func<string> generator)
{
    var configuredValue = builder.Configuration[key];
    return string.IsNullOrWhiteSpace(configuredValue) ? generator() : configuredValue;
}

static string GenerateSecret(int byteCount = 48)
    => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));

var postgresUser = builder.AddParameter(
    "postgres-user",
    GetConfiguredOrDefault("Parameters:PostgresUser", "skishop"));
var postgresPassword = builder.AddParameter(
    "postgres-password",
    GetConfiguredOrDefault("Parameters:PostgresPassword", "skishop_dev_password"),
    secret: true);
var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    GetConfiguredOrGenerated("Parameters:JwtSigningKey", () => GenerateSecret()),
    secret: true);
var authEncryptionKey = builder.AddParameter(
    "auth-encryption-key",
    GetConfiguredOrGenerated("Parameters:AuthEncryptionKey", () => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
    secret: true);
var dataExportEncryptionKey = builder.AddParameter(
    "data-export-encryption-key",
    GetConfiguredOrGenerated("Parameters:DataExportEncryptionKey", () => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
    secret: true);
var userManagementApiKey = builder.AddParameter(
    "user-management-api-key",
    GetConfiguredOrGenerated("Parameters:UserManagementApiKey", () => GenerateSecret(24)),
    secret: true);
var mcpServerApiKey = builder.AddParameter(
    "mcp-server-api-key",
    GetConfiguredOrGenerated("Parameters:McpServerApiKey", () => GenerateSecret()),
    secret: true);
var stripeSecretKey = builder.AddParameter(
    "stripe-secret-key",
    GetConfiguredOrGenerated("Parameters:StripeSecretKey", () => $"sk_test_{Guid.NewGuid():N}"),
    secret: true);
var stripeWebhookSecret = builder.AddParameter(
    "stripe-webhook-secret",
    GetConfiguredOrGenerated("Parameters:StripeWebhookSecret", () => $"whsec_{Guid.NewGuid():N}"),
    secret: true);
var sendGridApiKey = builder.AddParameter(
    "sendgrid-api-key",
    GetConfiguredOrGenerated("Parameters:SendGridApiKey", () => $"SG.{Guid.NewGuid():N}"),
    secret: true);
var azureOpenAIApiKey = builder.AddParameter(
    "azure-openai-api-key",
    GetConfiguredOrGenerated("Parameters:AzureOpenAIApiKey", () => GenerateSecret(24)),
    secret: true);
var azureAiSearchApiKey = builder.AddParameter(
    "azure-ai-search-api-key",
    GetConfiguredOrGenerated("Parameters:AzureAISearchApiKey", () => GenerateSecret(24)),
    secret: true);

var azureOpenAiEndpoint = GetConfiguredOrDefault("Parameters:AzureOpenAIEndpoint", "https://example.openai.azure.com");
var azureOpenAiDeploymentName = GetConfiguredOrDefault("Parameters:AzureOpenAIDeploymentName", "gpt-5");
var azureOpenAiEmbeddingDeploymentName = GetConfiguredOrDefault("Parameters:AzureOpenAIEmbeddingDeploymentName", "text-embedding-3-small");
var azureAiSearchEndpoint = GetConfiguredOrDefault("Parameters:AzureAISearchEndpoint", "https://example.search.windows.net");
var azureAiSearchIndexName = GetConfiguredOrDefault("Parameters:AzureAISearchIndexName", "products");
var azureCommunicationEndpoint = GetConfiguredOrDefault("Parameters:AzureCommunicationEndpoint", "https://example.communication.azure.com");
var azureCommunicationSenderAddress = GetConfiguredOrDefault("Parameters:AzureCommunicationSenderAddress", "no-reply@skishop.local");

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword, PostgresPort)
    .WithDataVolume();
var authDatabase = postgres.AddDatabase("auth-db", "authdb");
var userDatabase = postgres.AddDatabase("user-db", "userdb");
var inventoryDatabase = postgres.AddDatabase("inventory-db", "inventorydb");
var salesDatabase = postgres.AddDatabase("sales-db", "salesdb");
var cartDatabase = postgres.AddDatabase("cart-db", "cartdb");
var couponDatabase = postgres.AddDatabase("coupon-db", "coupondb");
var pointDatabase = postgres.AddDatabase("point-db", "pointdb");
var mailDatabase = postgres.AddDatabase("mail-db", "mailsenddb");
var aiDatabase = postgres.AddDatabase("ai-db", "aisupportdb");

var redis = builder.AddRedis("redis", RedisPort)
    .WithPassword(null)
    .WithDataVolume();
var kafka = builder.AddKafka("kafka", KafkaPort)
    .WithDataVolume()
    .WithKafkaUI(kafkaUi => kafkaUi.WithHostPort(KafkaUiPort));

var mailHog = builder.AddContainer("mailhog", "mailhog/mailhog", "v1.0.1")
    .WithHttpEndpoint(port: MailHogUiPort, targetPort: MailHogUiPort, name: "ui", isProxied: false)
    .WithEndpoint(port: MailHogSmtpPort, targetPort: MailHogSmtpPort, scheme: "tcp", name: "smtp", isProxied: false);

var authService = builder.AddProject<Projects.AuthService>("auth-service")
    .WithHttpEndpoint(port: AuthPort, targetPort: AuthPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", authBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", authDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("Encryption__Key", authEncryptionKey)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var userManagementService = builder.AddProject<Projects.UserManagementService>("user-management-service")
    .WithHttpEndpoint(port: UserManagementPort, targetPort: UserManagementPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", userManagementBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", userDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("DataExport__EncryptionKeyBase64", dataExportEncryptionKey)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var inventoryManagementService = builder.AddProject<Projects.InventoryManagementService>("inventory-management-service")
    .WithHttpEndpoint(port: InventoryPort, targetPort: InventoryPort, name: "rest", isProxied: false)
    .WithEndpoint(port: InventoryGrpcPort, targetPort: InventoryGrpcPort, scheme: "http", name: "grpc", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__DefaultConnection", inventoryDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var paymentCartService = builder.AddProject<Projects.PaymentCartService>("payment-cart-service")
    .WithHttpEndpoint(port: PaymentCartPort, targetPort: PaymentCartPort, name: "rest", isProxied: false)
    .WithEndpoint(port: PaymentCartGrpcPort, targetPort: PaymentCartGrpcPort, scheme: "http", name: "grpc", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__DefaultConnection", cartDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("Stripe__SecretKey", stripeSecretKey)
    .WithEnvironment("Stripe__WebhookSecret", stripeWebhookSecret)
    .WithEnvironment("Stripe__SuccessUrl", frontendSuccessUrl)
    .WithEnvironment("Stripe__CancelUrl", frontendCancelUrl)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var couponService = builder.AddProject<Projects.CouponService>("coupon-service")
    .WithHttpEndpoint(port: CouponPort, targetPort: CouponPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", couponBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", couponDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var pointService = builder.AddProject<Projects.PointService>("point-service")
    .WithHttpEndpoint(port: PointPort, targetPort: PointPort, name: "rest", isProxied: false)
    .WithEndpoint(port: PointGrpcPort, targetPort: PointGrpcPort, scheme: "http", name: "grpc", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__DefaultConnection", pointDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

var salesManagementService = builder.AddProject<Projects.SalesManagementService>("sales-management-service")
    .WithHttpEndpoint(port: SalesPort, targetPort: SalesPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", salesBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", salesDatabase)
    .WithEnvironment("ConnectionStrings__salesdb", salesDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("ConnectionStrings__redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("GrpcEndpoints__Inventory", inventoryGrpcBaseUrl)
    .WithEnvironment("GrpcEndpoints__Payment", paymentCartGrpcBaseUrl)
    .WithEnvironment("GrpcEndpoints__Cart", paymentCartGrpcBaseUrl)
    .WithEnvironment("GrpcEndpoints__Point", pointGrpcBaseUrl)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka)
    .WaitFor(inventoryManagementService)
    .WaitFor(paymentCartService)
    .WaitFor(pointService)
    .WaitFor(couponService);

var mailSendService = builder.AddProject<Projects.MailSendService>("mailsend-service")
    .WithHttpEndpoint(port: MailSendPort, targetPort: MailSendPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", mailSendBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", mailDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Services__UserManagement__Url", userManagementBaseUrl)
    .WithEnvironment("Services__UserManagement__ApiKey", userManagementApiKey)
    .WithEnvironment("SendGrid__ApiKey", sendGridApiKey)
    .WithEnvironment("SendGrid__BaseUrl", "https://api.sendgrid.com")
    .WithEnvironment("Mail__BaseUrl", frontendBaseUrl)
    .WithEnvironment("Azure__Communication__Endpoint", azureCommunicationEndpoint)
    .WithEnvironment("Azure__Communication__SenderAddress", azureCommunicationSenderAddress)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("Jwt__ExpirationMinutes", "60")
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka)
    .WaitFor(userManagementService);

var aiSupportService = builder.AddProject<Projects.AiSupportService>("ai-support-service")
    .WithHttpEndpoint(port: AiSupportPort, targetPort: AiSupportPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", aiSupportBaseUrl)
    .WithEnvironment("ConnectionStrings__DefaultConnection", aiDatabase)
    .WithEnvironment("ConnectionStrings__Redis", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Services__InventoryManagementService", inventoryBaseUrl)
    .WithEnvironment("Services__SalesManagementService", salesBaseUrl)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("AzureOpenAI__Endpoint", azureOpenAiEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey", azureOpenAIApiKey)
    .WithEnvironment("AzureOpenAI__DeploymentName", azureOpenAiDeploymentName)
    .WithEnvironment("AzureOpenAI__EmbeddingDeploymentName", azureOpenAiEmbeddingDeploymentName)
    .WithEnvironment("AzureAISearch__Endpoint", azureAiSearchEndpoint)
    .WithEnvironment("AzureAISearch__ApiKey", azureAiSearchApiKey)
    .WithEnvironment("AzureAISearch__IndexName", azureAiSearchIndexName)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka)
    .WaitFor(inventoryManagementService)
    .WaitFor(salesManagementService);

// Aspire MCP hosting is still marked experimental in 13.2.2; revisit when the API is promoted to GA.
#pragma warning disable ASPIREMCP001
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server")
    .WithHttpEndpoint(port: McpServerPort, targetPort: McpServerPort, name: "http", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", mcpServerBaseUrl)
    .WithEnvironment("McpServer__Security__ApiKey", mcpServerApiKey)
    .WithEnvironment("Services__InventoryManagement__BaseUrl", inventoryBaseUrl)
    .WaitFor(inventoryManagementService)
    .WithMcpServer(endpointName: "http");
#pragma warning restore ASPIREMCP001

var apiGateway = builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: ApiGatewayPort, targetPort: ApiGatewayPort, name: "rest", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", apiGatewayBaseUrl)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Jwt__SecretKey", jwtSigningKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Jwt__Key", jwtSigningKey)
    .WithEnvironment("Kafka__BootstrapServers", kafka)
    .WithEnvironment("Redis__ConnectionString", redis)
    .WithEnvironment("Cors__AllowedOrigins__0", frontendBaseUrl)
    .WithEnvironment("Cors__AllowedOrigins__1", adminPortalBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__auth-cluster__Destinations__destination1__Address", authBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__user-cluster__Destinations__destination1__Address", userManagementBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__inventory-cluster__Destinations__destination1__Address", inventoryBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__sales-cluster__Destinations__destination1__Address", salesBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__payment-cluster__Destinations__destination1__Address", paymentCartBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__coupon-cluster__Destinations__destination1__Address", couponBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__point-cluster__Destinations__destination1__Address", pointBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__mail-cluster__Destinations__destination1__Address", mailSendBaseUrl)
    .WithEnvironment("ReverseProxy__Clusters__ai-support-cluster__Destinations__destination1__Address", aiSupportBaseUrl)
    .WaitFor(redis)
    .WaitFor(kafka)
    .WaitFor(authService)
    .WaitFor(userManagementService)
    .WaitFor(inventoryManagementService)
    .WaitFor(salesManagementService)
    .WaitFor(paymentCartService)
    .WaitFor(couponService)
    .WaitFor(pointService)
    .WaitFor(mailSendService)
    .WaitFor(aiSupportService);

var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithHttpEndpoint(port: FrontendPort, targetPort: FrontendPort, name: "web", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", frontendBaseUrl)
    .WithEnvironment("ApiGateway__BaseUrl", apiGatewayBaseUrl)
    .WaitFor(apiGateway);

var adminPortal = builder.AddProject<Projects.AdminPortal>("admin-portal")
    .WithHttpEndpoint(port: AdminPortalPort, targetPort: AdminPortalPort, name: "web", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", adminPortalBaseUrl)
    .WithEnvironment("ApiGateway__BaseUrl", apiGatewayBaseUrl)
    .WaitFor(apiGateway);

builder.Build().Run();
