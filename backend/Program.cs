using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
String connectionString = "Data Source=db.db";
var sqlConnection = new SqliteConnection(connectionString);
try
{
    sqlConnection.Open();
    Console.WriteLine("SQL Database connected");
}
catch (SqliteException e)
{
    Console.WriteLine(e.Message);
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }

app.UseHttpsRedirection();

app.MapGet("/clientes/{id}", (int id) => $"Getting client {id}").WithName("getClient");
app.MapPost("/clientes", (CreateClientRequest new_client) =>
{
    // string forma_da_tabela_query = "PRAGMA table_info(Membros)";
    // using SqliteCommand command = new (forma_da_tabela_query, sqlConnection);
    // using var reader  = command.ExecuteReader();
    // while (reader.Read())
    // {
    //     Console.WriteLine($"{reader["name"]} | {reader["type"]}");
    // }
    
    string query = @"INSERT INTO Membros (Nome, Cpf, Telefone, Endereco, data_cadastro)
                 VALUES ($nome, $cpf, $telefone, $endereco, $data);
                 SELECT last_insert_rowid();";

    using SqliteCommand command = new(query, sqlConnection);
    command.Parameters.AddWithValue("$nome", new_client.name);
    command.Parameters.AddWithValue("$cpf", new_client.cpf);
    command.Parameters.AddWithValue("$telefone", new_client.telefone);
    command.Parameters.AddWithValue("$endereco", new_client.address);
    command.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

    long id = (long)command.ExecuteScalar()!;
    // return Results.Created($"/clientes/{id}", new { id });
    
    return "Adding client: " + new_client.name + " " + new_client.cpf + " " + new_client.telefone + " " + new_client.address;
}).WithName("addClient");

app.Run();

record CreateClientRequest(string name, string cpf, string telefone, string address);