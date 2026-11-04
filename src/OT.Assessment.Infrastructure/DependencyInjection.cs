using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OT.Assessment.Domain.Interfaces;
using OT.Assessment.Infrastructure.Messaging;
using OT.Assessment.Infrastructure.Persistence;

namespace OT.Assessment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSqlRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("OT_Assessment_DB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The connection string 'OT_Assessment_DB' is missing from configuration.");
        }

        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddSingleton<ICasinoWagerRepository, SqlCasinoWagerRepository>();
        services.AddSingleton<IPlayerRepository, SqlPlayerRepository>();

        return services;
    }

    public static IServiceCollection AddRabbitMqOptions(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(section);

        return services;
    }

    public static IServiceCollection AddCasinoWagerPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqOptions(configuration);
        services.AddSingleton<ICasinoWagerPublisher, RabbitMqCasinoWagerPublisher>();

        return services;
    }
}
