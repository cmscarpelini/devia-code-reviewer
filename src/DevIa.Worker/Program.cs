using DevIa.Infrastructure;
using DevIa.Infrastructure.Ai;
using DevIa.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddReviewPipeline(builder.Configuration);
builder.Services.AddHostedService<ReviewJobConsumer>();

var host = builder.Build();
host.Run();
