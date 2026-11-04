using OT.Assessment.Consumer;
using OT.Assessment.Consumer.Batching;
using OT.Assessment.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

IConfigurationSection consumerSection = builder.Configuration.GetSection(ConsumerOptions.SectionName);
builder.Services.Configure<ConsumerOptions>(consumerSection);

builder.Services.AddRabbitMqOptions(builder.Configuration);
builder.Services.AddSqlRepositories(builder.Configuration);

builder.Services.AddSingleton<WagerBatchBuffer>();
builder.Services.AddHostedService<CasinoWagerConsumerWorker>();

var host = builder.Build();
host.Run();
