using Microsoft.SemanticKernel.Embeddings;
using RagRest;
using RagRest.Clients;
using RagRest.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ITextEmbeddingGenerationService, LocalNomicEmbedTextEmbeddingService>();

// Register application setting using IOption provider mechanism
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));
// Fetch settings object from configuration
var settings = new ApplicationSettings();
builder.Configuration.GetSection("ApplicationSettings").Bind(settings);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<MyNeo4jClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseAuthentication();
//app.UseAuthorization();

app.MapControllers();

app.Run();
