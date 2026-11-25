using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DbPath"] ?? "db.db";
if (!File.Exists(dbPath))
{
    Console.WriteLine($"Banco de dados não encontrado em {Path.GetFullPath(dbPath)}");
    Environment.Exit(1);
}

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

    // Sem Commit(), o Dispose faz rollback automático
    using var tx = sqlConnection.BeginTransaction();

    try
    {
        long fichaA = Utils.CriarFicha(sqlConnection, tx);
        long fichaB = Utils.CriarFicha(sqlConnection, tx);
        long fichaC = Utils.CriarFicha(sqlConnection, tx);

        string query = @"INSERT INTO Membros (Nome, Cpf, Telefone, Endereco, data_cadastro,
                                              id_treino_a, id_treino_b, id_treino_c)
                         VALUES ($nome, $cpf, $telefone, $endereco, $data, $a, $b, $c);
                         SELECT last_insert_rowid();";

        using SqliteCommand command = new(query, sqlConnection, tx);
        command.Parameters.AddWithValue("$nome", new_client.name);
        command.Parameters.AddWithValue("$cpf", new_client.cpf);
        command.Parameters.AddWithValue("$telefone", new_client.telefone);
        command.Parameters.AddWithValue("$endereco", new_client.address);
        command.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$a", fichaA);
        command.Parameters.AddWithValue("$b", fichaB);
        command.Parameters.AddWithValue("$c", fichaC);

        long id = (long)command.ExecuteScalar()!;
        tx.Commit();

        return Results.Created($"/clientes/{id}", new { id, id_treino_a = fichaA, id_treino_b = fichaB, id_treino_c = fichaC });
    }
    catch (SqliteException e) when (e.SqliteErrorCode == 19)
    {
        return Results.Conflict($"O CPF {new_client.cpf} já é usado por outro cliente");
    }
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

app.MapDelete("/treinos/{id_treino}/set/{id_set}", (int id_treino, int id_set) =>
{
    using SqliteCommand command = new(
        "DELETE FROM Treino_Sets WHERE id_treino = @treino AND id_set = @set",
        sqlConnection);
    command.Parameters.AddWithValue("@treino", id_treino);
    command.Parameters.AddWithValue("@set", id_set);

    int linhas = command.ExecuteNonQuery();

    return linhas == 0
        ? Results.NotFound($"O set {id_set} não pertence ao treino {id_treino}")
        : Results.NoContent();
}).WithName("removeSetFromTreino");

app.MapPost("/sets", (CreateSetRequest req) =>
{
    if (req.sets <= 0 || req.repeticoes <= 0)
        return Results.BadRequest("sets e repeticoes devem ser maiores que zero");

    string query = @"INSERT INTO Sets (id_exercicio, sets, repeticoes)
                     VALUES ($id_exercicio, $sets, $repeticoes);
                     SELECT last_insert_rowid();";

    using SqliteCommand command = new(query, sqlConnection);
    command.Parameters.AddWithValue("$id_exercicio", req.id_exercicio);
    command.Parameters.AddWithValue("$sets", req.sets);
    command.Parameters.AddWithValue("$repeticoes", req.repeticoes);

    try
    {
        long id = (long)command.ExecuteScalar()!;
        return Results.Created($"/sets/{id}", new { id });
    }
    catch (SqliteException e) when (e.SqliteErrorCode == 19)
    {
        return Results.BadRequest($"O exercício {req.id_exercicio} não existe");
    }
}).WithName("addSet");

app.MapPost("/treinos/{id_treino}/set/{id_set}", (int id_treino, int id_set) =>
{
    using SqliteCommand command = new(@"
        INSERT INTO Treino_Sets (id_treino, id_set, ordem)
        VALUES ($treino, $set,
                (SELECT COALESCE(MAX(ordem), 0) + 1
                 FROM Treino_Sets WHERE id_treino = $treino))",
        sqlConnection);
    command.Parameters.AddWithValue("$treino", id_treino);
    command.Parameters.AddWithValue("$set", id_set);

    try
    {
        command.ExecuteNonQuery();
        return Results.NoContent();
    }
    catch (SqliteException e) when (e.SqliteErrorCode == 19)
    {
        return Results.Conflict("Esse set já está no treino, ou o treino/set não existe");
    }
}).WithName("addSetToTreino");

app.MapGet("/exercicios", () =>
{
    using SqliteCommand command = new(
        "SELECT id, nome, descricao FROM Exercicios ORDER BY nome",
        sqlConnection);

    using var reader = command.ExecuteReader();

    var exercicios = new List<object>();
    while (reader.Read())
    {
        exercicios.Add(new
        {
            id = reader.GetInt64(0),
            nome = reader.GetString(1),
            descricao = reader.IsDBNull(2) ? null : reader.GetString(2)
        });
    }

    return Results.Ok(exercicios);
}).WithName("getExercicios");

app.MapGet("/exercicios/{id}", (int id) =>
{
    using SqliteCommand command = new(
        "SELECT id, nome, descricao FROM Exercicios WHERE id = @id",
        sqlConnection);
    command.Parameters.AddWithValue("@id", id);

    using var reader = command.ExecuteReader();

    if (!reader.Read())
        return Results.NotFound();

    return Results.Ok(new
    {
        id = reader.GetInt64(0),
        nome = reader.GetString(1),
        descricao = reader.IsDBNull(2) ? null : reader.GetString(2)
    });
}).WithName("getExercicio");

app.MapPost("/exercicios", (CreateExerciseRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.nome))
        return Results.BadRequest("Informe o nome do exercício");

    using SqliteCommand command = new(@"
        INSERT INTO Exercicios (nome, descricao)
        VALUES ($nome, $descricao);
        SELECT last_insert_rowid();",
        sqlConnection);
    command.Parameters.AddWithValue("$nome", req.nome);
    command.Parameters.AddWithValue("$descricao", (object?)req.descricao ?? DBNull.Value);

    long id = (long)command.ExecuteScalar()!;
    return Results.Created($"/exercicios/{id}", new { id });
}).WithName("addExercicio");

app.MapPut("/exercicios/{id}", (int id, UpdateExerciseRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.nome))
        return Results.BadRequest("Informe o nome do exercício");

    using SqliteCommand command = new(@"
        UPDATE Exercicios SET nome = $nome, descricao = $descricao
        WHERE id = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id);
    command.Parameters.AddWithValue("$nome", req.nome);
    command.Parameters.AddWithValue("$descricao", (object?)req.descricao ?? DBNull.Value);

    return command.ExecuteNonQuery() == 0
        ? Results.NotFound()
        : Results.NoContent();
}).WithName("updateExercicio");

app.MapDelete("/exercicios/{id}", (int id) =>
{
    // Checagem manual: não depende de Foreign Keys=True na connection string
    using (SqliteCommand check = new(
               "SELECT COUNT(*) FROM Sets WHERE id_exercicio = $id", sqlConnection))
    {
        check.Parameters.AddWithValue("$id", id);
        if ((long)check.ExecuteScalar()! > 0)
            return Results.Conflict("Esse exercício é usado por um ou mais sets e não pode ser removido");
    }

    using SqliteCommand command = new("DELETE FROM Exercicios WHERE id = $id", sqlConnection);
    command.Parameters.AddWithValue("$id", id);

    return command.ExecuteNonQuery() == 0
        ? Results.NotFound()
        : Results.NoContent();
}).WithName("removeExercicio");

app.MapGet("/pagamentos", () =>
{
    using SqliteCommand command = new(@"
        SELECT id, id_membro, id_funcionario, valor, data_pagamento, status
        FROM Pagamentos
        ORDER BY data_pagamento DESC, id DESC",
        sqlConnection);

    using var reader = command.ExecuteReader();

    var pagamentos = new List<object>();
    while (reader.Read())
        pagamentos.Add(Utils.LerPagamento(reader));

    return Results.Ok(pagamentos);
}).WithName("getPagamentos");

app.MapGet("/pagamentos/{id_cliente}", (int id_cliente) =>
{
    if (Utils.GetAmountOfMembrosWithId(id_cliente, sqlConnection) == 0)
        return Results.NotFound($"O cliente {id_cliente} não existe");

    using SqliteCommand command = new(@"
        SELECT id, id_membro, id_funcionario, valor, data_pagamento, status
        FROM Pagamentos
        WHERE id_membro = $id
        ORDER BY data_pagamento DESC, id DESC",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id_cliente);

    using var reader = command.ExecuteReader();

    var pagamentos = new List<object>();
    while (reader.Read())
        pagamentos.Add(Utils.LerPagamento(reader));

    return Results.Ok(pagamentos);
}).WithName("getPagamentosCliente");

app.MapPost("/pagamentos", (CreatePaymentRequest req) =>
{
    if (req.valor < 0)
        return Results.BadRequest("O valor não pode ser negativo");

    if (string.IsNullOrWhiteSpace(req.status))
        return Results.BadRequest("Informe o status do pagamento");

    if (Utils.GetAmountOfMembrosWithId(req.id_membro, sqlConnection) == 0)
        return Results.NotFound($"O cliente {req.id_membro} não existe");

    if (Utils.GetAmountOfFuncionariosWithId(req.id_funcionario, sqlConnection) == 0)
        return Results.NotFound($"O funcionário {req.id_funcionario} não existe");

    using SqliteCommand command = new(@"
        INSERT INTO Pagamentos (id_membro, id_funcionario, valor, data_pagamento, status)
        VALUES ($membro, $funcionario, $valor, $data, $status);
        SELECT last_insert_rowid();",
        sqlConnection);
    command.Parameters.AddWithValue("$membro", req.id_membro);
    command.Parameters.AddWithValue("$funcionario", req.id_funcionario);
    command.Parameters.AddWithValue("$valor", req.valor);
    command.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("$status", req.status);

    long id = (long)command.ExecuteScalar()!;
    return Results.Created($"/pagamentos/{req.id_membro}", new { id });
}).WithName("addPagamento");

app.MapGet("/assinaturas", () =>
{
    using SqliteCommand command = new(
        "SELECT id_membro, data_final FROM Validade_Assinaturas ORDER BY data_final",
        sqlConnection);

    using var reader = command.ExecuteReader();

    var assinaturas = new List<object>();
    while (reader.Read())
        assinaturas.Add(Utils.LerAssinatura(reader));

    return Results.Ok(assinaturas);
}).WithName("getAssinaturas");

app.MapGet("/assinaturas/{id_membro}", (int id_membro) =>
{
    using SqliteCommand command = new(
        "SELECT id_membro, data_final FROM Validade_Assinaturas WHERE id_membro = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id_membro);

    using var reader = command.ExecuteReader();

    if (!reader.Read())
        return Results.NotFound($"O cliente {id_membro} não tem assinatura cadastrada");

    return Results.Ok(Utils.LerAssinatura(reader));
}).WithName("getAssinatura");

app.MapPost("/assinaturas", (CreateSubscriptionRequest req) =>
{
    if (!Utils.TryParseData(req.data_final, out DateOnly dataFinal))
        return Results.BadRequest("data_final deve estar no formato yyyy-MM-dd");

    if (Utils.GetAmountOfMembrosWithId(req.id_membro, sqlConnection) == 0)
        return Results.NotFound($"O cliente {req.id_membro} não existe");

    using SqliteCommand command = new(
        "INSERT INTO Validade_Assinaturas (id_membro, data_final) VALUES ($id, $data)",
        sqlConnection);
    command.Parameters.AddWithValue("$id", req.id_membro);
    command.Parameters.AddWithValue("$data", dataFinal.ToString("yyyy-MM-dd"));

    try
    {
        command.ExecuteNonQuery();
        return Results.Created($"/assinaturas/{req.id_membro}", new { id_membro = req.id_membro });
    }
    catch (SqliteException e) when (e.SqliteErrorCode == 19)
    {
        // PRIMARY KEY duplicada: o cliente já tem assinatura
        return Results.Conflict($"O cliente {req.id_membro} já tem assinatura. Use PUT para alterar a validade");
    }
}).WithName("addAssinatura");

app.MapPut("/assinaturas/{id_membro}", (int id_membro, UpdateSubscriptionRequest req) =>
{
    if (!Utils.TryParseData(req.data_final, out DateOnly dataFinal))
        return Results.BadRequest("data_final deve estar no formato yyyy-MM-dd");

    using SqliteCommand command = new(
        "UPDATE Validade_Assinaturas SET data_final = $data WHERE id_membro = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id_membro);
    command.Parameters.AddWithValue("$data", dataFinal.ToString("yyyy-MM-dd"));

    return command.ExecuteNonQuery() == 0
        ? Results.NotFound($"O cliente {id_membro} não tem assinatura cadastrada")
        : Results.NoContent();
}).WithName("updateAssinatura");

app.MapDelete("/assinaturas/{id_membro}", (int id_membro) =>
{
    using SqliteCommand command = new(
        "DELETE FROM Validade_Assinaturas WHERE id_membro = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id_membro);

    return command.ExecuteNonQuery() == 0
        ? Results.NotFound($"O cliente {id_membro} não tem assinatura cadastrada")
        : Results.NoContent();
}).WithName("removeAssinatura");
app.MapGet("/historico-treinos", (int? id_membro) =>
{
    using SqliteCommand command = new(@"
        SELECT id, id_membro, id_treino, dia
        FROM Historico_Treinos
        WHERE $membro IS NULL OR id_membro = $membro
        ORDER BY dia DESC, id DESC",
        sqlConnection);
    command.Parameters.AddWithValue("$membro", (object?)id_membro ?? DBNull.Value);

    using var reader = command.ExecuteReader();

    var registros = new List<object>();
    while (reader.Read())
        registros.Add(Utils.LerHistoricoTreino(reader));

    return Results.Ok(registros);
}).WithName("getHistoricoTreinos");

app.MapGet("/historico-treinos/{id}", (int id) =>
{
    using SqliteCommand command = new(
        "SELECT id, id_membro, id_treino, dia FROM Historico_Treinos WHERE id = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id);

    using var reader = command.ExecuteReader();

    if (!reader.Read())
        return Results.NotFound();

    return Results.Ok(Utils.LerHistoricoTreino(reader));
}).WithName("getHistoricoTreino");

app.MapPost("/historico-treinos", (CreateTrainingLogRequest req) =>
{
    // Se o dia não for informado, usa hoje
    string diaTexto = req.dia ?? DateTime.Now.ToString("yyyy-MM-dd");
    if (!Utils.TryParseData(diaTexto, out DateOnly dia))
        return Results.BadRequest("dia deve estar no formato yyyy-MM-dd");

    if (Utils.GetAmountOfMembrosWithId(req.id_membro, sqlConnection) == 0)
        return Results.NotFound($"O cliente {req.id_membro} não existe");

    if (!Utils.FichaPertenceAoMembro(req.id_membro, req.id_treino, sqlConnection))
        return Results.BadRequest($"O treino {req.id_treino} não é uma ficha do cliente {req.id_membro}");

    using SqliteCommand command = new(@"
        INSERT INTO Historico_Treinos (id_membro, id_treino, dia)
        VALUES ($membro, $treino, $dia);
        SELECT last_insert_rowid();",
        sqlConnection);
    command.Parameters.AddWithValue("$membro", req.id_membro);
    command.Parameters.AddWithValue("$treino", req.id_treino);
    command.Parameters.AddWithValue("$dia", dia.ToString("yyyy-MM-dd"));

    long id = (long)command.ExecuteScalar()!;
    return Results.Created($"/historico-treinos/{id}", new { id });
}).WithName("addHistoricoTreino");

app.MapPut("/historico-treinos/{id}", (int id, UpdateTrainingLogRequest req) =>
{
    if (!Utils.TryParseData(req.dia, out DateOnly dia))
        return Results.BadRequest("dia deve estar no formato yyyy-MM-dd");

    // Descobre o membro dono do registro (também confirma que ele existe)
    long idMembro;
    using (SqliteCommand select = new(
               "SELECT id_membro FROM Historico_Treinos WHERE id = $id", sqlConnection))
    {
        select.Parameters.AddWithValue("$id", id);
        object? result = select.ExecuteScalar();
        if (result is null)
            return Results.NotFound();
        idMembro = (long)result;
    }

    if (!Utils.FichaPertenceAoMembro(idMembro, req.id_treino, sqlConnection))
        return Results.BadRequest($"O treino {req.id_treino} não é uma ficha do cliente {idMembro}");

    using SqliteCommand command = new(
        "UPDATE Historico_Treinos SET id_treino = $treino, dia = $dia WHERE id = $id",
        sqlConnection);
    command.Parameters.AddWithValue("$id", id);
    command.Parameters.AddWithValue("$treino", req.id_treino);
    command.Parameters.AddWithValue("$dia", dia.ToString("yyyy-MM-dd"));
    command.ExecuteNonQuery();

    return Results.NoContent();
}).WithName("updateHistoricoTreino");

app.MapDelete("/historico-treinos/{id}", (int id) =>
{
    using SqliteCommand command = new(
        "DELETE FROM Historico_Treinos WHERE id = $id", sqlConnection);
    command.Parameters.AddWithValue("$id", id);

    return command.ExecuteNonQuery() == 0
        ? Results.NotFound()
        : Results.NoContent();
}).WithName("removeHistoricoTreino");

app.Run();

public partial class Program { }

record CreateClientRequest(string name, string cpf, string telefone, string address);
record CreateEmployeeRequest(string name, string role, string login, string password);
record LoginRequest(string login, string password);
record CreateSetRequest(int id_exercicio, int sets, int repeticoes);
record CreateExerciseRequest(string nome, string? descricao);
record UpdateExerciseRequest(string nome, string? descricao);
record CreatePaymentRequest(int id_membro, int id_funcionario, decimal valor, string status);
record CreateSubscriptionRequest(int id_membro, string data_final);
record UpdateSubscriptionRequest(string data_final);
record CreateTrainingLogRequest(int id_membro, int id_treino, string? dia);
record UpdateTrainingLogRequest(int id_treino, string dia);

class Utils
{
    public static long GetAmountOfMembrosWithCPF(string cpf, SqliteConnection sqlConnection)
    {
        using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Membros WHERE cpf = @cpf";
        command.Parameters.AddWithValue("@cpf", cpf);

        return (long)command.ExecuteScalar()!;
    }    
    public static long CriarFicha(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = new SqliteCommand(
            "INSERT INTO Treino DEFAULT VALUES; SELECT last_insert_rowid();",
            conn, tx);
        return (long)cmd.ExecuteScalar()!;
    }
    public static long GetAmountOfMembrosWithId(int id, SqliteConnection sqlConnection)
    {
        using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Membros WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (long)command.ExecuteScalar()!;
    }

    public static long GetAmountOfFuncionariosWithId(int id, SqliteConnection sqlConnection)
    {
        using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Funcionarios WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (long)command.ExecuteScalar()!;
    }

    public static object LerPagamento(SqliteDataReader reader) => new
    {
        id = reader.GetInt64(0),
        id_membro = reader.GetInt64(1),
        id_funcionario = reader.GetInt64(2),
        valor = reader.GetValue(3),
        data_pagamento = reader.GetString(4),
        status = reader.GetString(5)
    };
    public static bool TryParseData(string? texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out data);

    public static object LerAssinatura(SqliteDataReader reader)
    {
        string dataFinal = reader.GetString(1);
        return new
        {
            id_membro = reader.GetInt64(0),
            data_final = dataFinal,
            // yyyy-MM-dd compara corretamente como texto; vale até o fim do dia data_final
            ativa = string.CompareOrdinal(dataFinal, DateTime.Now.ToString("yyyy-MM-dd")) >= 0
        };
    }
    public static bool FichaPertenceAoMembro(long idMembro, long idTreino, SqliteConnection sqlConnection)
    {
        using var command = sqlConnection.CreateCommand();
        command.CommandText = @"
        SELECT COUNT(*) FROM Membros
        WHERE id = $membro
          AND ($treino = id_treino_a OR $treino = id_treino_b OR $treino = id_treino_c)";
        command.Parameters.AddWithValue("$membro", idMembro);
        command.Parameters.AddWithValue("$treino", idTreino);
        return (long)command.ExecuteScalar()! > 0;
    }

    public static object LerHistoricoTreino(SqliteDataReader reader) => new
    {
        id = reader.GetInt64(0),
        id_membro = reader.GetInt64(1),
        id_treino = reader.GetInt64(2),
        dia = reader.GetString(3)
    };
}
