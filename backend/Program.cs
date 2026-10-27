var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }

app.UseHttpsRedirection();

app.MapGet("/clients/{id}", (int id) => $"Getting client {id}").WithName("getClient");
app.MapPost("/clients", (CreateClientRequest new_client) =>
{
    return "Adding client: " + new_client.name + " " + new_client.cpf + " " + new_client.telefone + " " + new_client.address;
}).WithName("addClient");

app.Run();

record CreateClientRequest(string name, string cpf, string telefone, string address);