using AgenticUrlShortener.Infrastructure.Persistence;
using AgenticUrlShortener.Application.Services;
using AgenticUrlShortener.Orchestration.Core;
using AgenticUrlShortener.Orchestration.Stages;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

// Register repositories
builder.Services.AddSingleton<IUrlRepository, InMemoryUrlRepository>();
builder.Services.AddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();

// Register business services
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Register orchestration engine and stages
builder.Services.AddSingleton<IWorkflowOrchestrator>(sp =>
{
    var repo = sp.GetRequiredService<IWorkflowRepository>();
    var orchestrator = new WorkflowOrchestrator(repo);
    
    // Register workflow stages
    var urlRepo = sp.GetRequiredService<IUrlRepository>();
    orchestrator.RegisterStage(new ValidateAndNormalizeUrlStage());
    orchestrator.RegisterStage(new CheckDuplicateAndReserveStage(urlRepo));
    orchestrator.RegisterStage(new PersistUrlStage(urlRepo));
    orchestrator.RegisterStage(new GenerateResponseStage());
    orchestrator.RegisterStage(new ValidateAndUpdateAccessStage(urlRepo));
    orchestrator.RegisterStage(new PrepareRedirectStage());
    orchestrator.RegisterStage(new DeleteUrlStage(urlRepo));
    
    return orchestrator;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
