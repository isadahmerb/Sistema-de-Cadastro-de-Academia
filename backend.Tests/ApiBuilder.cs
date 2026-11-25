using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace backend.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private static long _seq;

    public string DbPath { get; } =
        Path.Combine(Path.GetTempPath(), $"db.db");

    public ApiFactory()
    {
        CriarBanco();
    }

    

    private void CriarBanco()
    {
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema.sql"));

        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = schema;
        cmd.ExecuteNonQuery();
    }

    // O Program.cs lê "DbPath" logo no início, então a variável de ambiente
    // precisa existir antes de o host ser criado.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("DbPath", DbPath);
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        SqliteConnection.ClearAllPools();
        try { File.Delete(DbPath); } catch (IOException) { /* ainda em uso: ignora */ }
    }

    public SqliteConnection AbrirConexao()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    public void Limpar()
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM Historico_Treinos;
            DELETE FROM Historico_Atividades;
            DELETE FROM Validade_Assinaturas;
            DELETE FROM Pagamentos;
            DELETE FROM Treinos;
            DELETE FROM Treino_Sets;
            DELETE FROM Membros;
            DELETE FROM Treino;
            DELETE FROM Sets;
            DELETE FROM Exercicios;
            DELETE FROM Funcionarios;
            DELETE FROM Current_sessions;
            DELETE FROM sqlite_sequence;
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Insere um membro direto no banco e devolve o id. CPF único automático se não for informado.</summary>
    public long AddCliente(string nome = "Ana", string? cpf = null,
                           string telefone = "48999999999", string endereco = "Rua A, 1")
    {
        cpf ??= Interlocked.Increment(ref _seq).ToString("D11");

        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Membros (nome, cpf, telefone, endereco, data_cadastro)
            VALUES ($nome, $cpf, $tel, $end, $data);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$nome", nome);
        cmd.Parameters.AddWithValue("$cpf", cpf);
        cmd.Parameters.AddWithValue("$tel", telefone);
        cmd.Parameters.AddWithValue("$end", endereco);
        cmd.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return (long)cmd.ExecuteScalar()!;
    }
}