using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Confluent.Kafka;
using StackExchange.Redis;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataExport:EncryptionKeyBase64"] = Convert.ToBase64String(new byte[32]),
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:SigningKey"] = "test-signing-key-with-32-chars-min!"
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
            var mockDatabase = Substitute.For<IDatabase>();
            mockMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(mockDatabase);
            services.AddSingleton(mockMultiplexer);

            var producerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProducer<string, string>));
            if (producerDescriptor is not null)
                services.Remove(producerDescriptor);
            services.AddSingleton(Substitute.For<IProducer<string, string>>());

            var factoryDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IKafkaConsumerFactory));
            if (factoryDescriptor is not null)
                services.Remove(factoryDescriptor);
            services.AddSingleton(Substitute.For<IKafkaConsumerFactory>());

            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServiceDescriptors)
                services.Remove(descriptor);

            services.AddAuthentication(FakeAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(
                    FakeAuthHandler.SchemeName, _ => { });
        });

        builder.UseEnvironment("Testing");
    }
}
