using OT.Assessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Unhandled exceptions, like RabbitMQ being down, return a standard ProblemDetails 500 response.
builder.Services.AddProblemDetails();

builder.Services.AddCasinoWagerPublisher(builder.Configuration);
builder.Services.AddSqlRepositories(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
