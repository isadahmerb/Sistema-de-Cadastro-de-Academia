using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

var dbPath = "db.db";
if (!File.Exists(dbPath))
{
    Console.WriteLine($"Banco de dados não encontrado em {Path.GetFullPath(dbPath)}");
    Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder(args);
String connectionString = $"Data Source={dbPath};Mode=ReadWrite";
var sqlConnection = new SqliteConnection(connectionString);
try
{
    sqlConnection.Open();
    Console.WriteLine("SQL Database connected");
}
catch (SqliteException e)
{
    Console.WriteLine(e.Message);
    Environment.Exit(1);
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }

app.UseHttpsRedirection();

app.MapGet("/clientes/{id}", (int id) =>
{
    // TODO: Validação das informações recebidas

    string query = @"SELECT * FROM Membros WHERE id = @id";

    using SqliteCommand command = new(query, sqlConnection);
    command.Parameters.AddWithValue("@id", id);
    
    using var reader = command.ExecuteReader();

    if (!reader.Read())
        return Results.NotFound();

    Dictionary<string, object?> cliente = new();
    for (int i = 0; i < reader.FieldCount; i++)
        cliente.Add(reader.GetName(i), reader.GetValue(i));
    
    return Results.Ok(cliente);
}).WithName("getClient");
app.MapGet("/clientes", () =>
{
    // TODO: Validação das informações recebidas

    string query = @"SELECT * FROM Membros";

    using SqliteCommand command = new(query, sqlConnection);
    
    using var reader = command.ExecuteReader();

    List<Dictionary<string, object?>> clientes = new();
    while (reader.Read())
    {
        var cliente = new Dictionary<string, object?>();

        for (int i = 0; i < reader.FieldCount; i++)
            cliente[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

        clientes.Add(cliente);
    }
    
    return Results.Ok(clientes);
}).WithName("getClients");

app.MapPost("/clientes", (CreateClientRequest new_client) =>
{
    // TODO: Validação das informações recebidas

    if (Utils.GetAmountOfMembrosWithCPF(new_client.cpf, sqlConnection) > 0)
        return Results.Conflict($"O CPF {new_client.cpf} já é usado por outro cliente");
    
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
    return Results.Created($"/clientes/{id}", new { id });
    
    // return "Adding client: " + new_client.name + " " + new_client.cpf + " " + new_client.telefone + " " + new_client.address;
}).WithName("addClient");

app.MapPost("/employee", (CreateEmployeeRequest new_employee) =>
{
    // TODO: Validação das informações recebidas

    string query = @"INSERT INTO Funcionarios (nome, cargo, login, senha_hash, senha_salt)
                 VALUES ($nome, $cargo, $login, $senha_hash, $senha_salt);
                 SELECT last_insert_rowid();";

    byte[] salt = RandomNumberGenerator.GetBytes(16);
    byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
        new_employee.password,
        salt,
        100_000,
        HashAlgorithmName.SHA256,
        32);

    using SqliteCommand command = new(query, sqlConnection);
    command.Parameters.AddWithValue("$nome", new_employee.name);
    command.Parameters.AddWithValue("$cargo", new_employee.role);
    command.Parameters.AddWithValue("$login", new_employee.login);
    command.Parameters.AddWithValue("$senha_hash", Convert.ToBase64String(hash));
    command.Parameters.AddWithValue("$senha_salt", Convert.ToBase64String(salt));

    try
    {
        long id = (long)command.ExecuteScalar()!;
        return Results.Created($"/employee/{id}", new { id });
    }
    catch (SqliteException e) when (e.SqliteErrorCode == 19)
    {
        return Results.Conflict($"O login {new_employee.login} já é usado por outro funcionário");
    }
}).WithName("addEmployee");

app.MapPost("/login", (LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.login) || string.IsNullOrEmpty(request.password))
        return Results.BadRequest("Informe login e senha");

    byte[] storedHash;
    byte[] salt;

    {
        using SqliteCommand command = new(
            "SELECT senha_hash, senha_salt FROM Funcionarios WHERE login = $login",
            sqlConnection);
        command.Parameters.AddWithValue("$login", request.login);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return Results.Unauthorized();

        storedHash = Convert.FromBase64String(reader.GetString(0));
        salt = Convert.FromBase64String(reader.GetString(1));
    }

    byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
        request.password,
        salt,
        100_000,
        HashAlgorithmName.SHA256,
        storedHash.Length);

    if (!CryptographicOperations.FixedTimeEquals(computedHash, storedHash))
        return Results.Unauthorized();

    string token = RandomNumberGenerator.GetHexString(32, lowercase: true);

        using SqliteCommand insert = new(
        @"INSERT INTO Current_sessions (session_token, last_used)
          VALUES ($token, CURRENT_TIMESTAMP)",
        sqlConnection);
    insert.Parameters.AddWithValue("$token", token);
    insert.ExecuteNonQuery();

    return Results.Ok(new { token });
}).WithName("login");

app.MapGet("/treinos/{id}", (int id) =>
{
    // Busca as 3 fichas (A, B, C) do membro
    long?[] fichas = new long?[3];

    using (SqliteCommand command = new(
               "SELECT id_treino_a, id_treino_b, id_treino_c FROM Membros WHERE id = @id",
               sqlConnection))
    {
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return Results.NotFound();

        for (int i = 0; i < 3; i++)
            fichas[i] = reader.IsDBNull(i) ? null : reader.GetInt64(i);
    }

    // Para cada ficha, busca os exercicios com seus sets
    var treinos = new List<object>();

    foreach (long? ficha in fichas)
    {
        var exercicios = new List<object>();

        if (ficha is not null)
        {
            using SqliteCommand command = new(@"
                SELECT ts.id_treino, ts.id_set, e.nome, e.descricao, s.sets, s.repeticoes
                FROM Treino_Sets ts
                JOIN Sets s ON s.id = ts.id_set
                JOIN Exercicios e ON e.id = s.id_exercicio
                WHERE ts.id_treino = @treino
                ORDER BY ts.ordem, ts.id_set",
                sqlConnection);
            command.Parameters.AddWithValue("@treino", ficha.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                exercicios.Add(new
                {
                    id_treino = reader.GetInt64(0),
                    id_set = reader.GetInt64(1),
                    nome = reader.GetString(2),
                    descricao = reader.IsDBNull(3) ? null : reader.GetString(3),
                    sets = new
                    {
                        sets = reader.GetInt64(4),
                        repeticoes = reader.GetInt64(5)
                    }
                });
            }
        }

        treinos.Add(new { exercicios });
    }

    return Results.Ok(new { treinos });
}).WithName("getTreinos");

app.Run();

record CreateClientRequest(string name, string cpf, string telefone, string address);
record CreateEmployeeRequest(string name, string role, string login, string password);
record LoginRequest(string login, string password);

record TreinosRequest(int membro_id);

class Utils
{
    public static long GetAmountOfMembrosWithCPF(string cpf, SqliteConnection sqlConnection)
    {
        using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Membros WHERE cpf = @cpf";
        command.Parameters.AddWithValue("@cpf", cpf);

        return (long)command.ExecuteScalar()!;
    }    
}
