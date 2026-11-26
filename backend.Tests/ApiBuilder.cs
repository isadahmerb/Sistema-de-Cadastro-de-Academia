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
        cpf ??= ProximoCpf();

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
    
    private static string ProximoCpf() => Interlocked.Increment(ref _seq).ToString("D11");

    public ClienteComFichas AddClienteComFichas(string nome = "Ana", string? cpf = null)
    {
        cpf ??= ProximoCpf();

        using var conn = AbrirConexao();
        using var tx = conn.BeginTransaction();

        long NovaFicha()
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Treino DEFAULT VALUES; SELECT last_insert_rowid();";
            return (long)cmd.ExecuteScalar()!;
        }

        long fichaA = NovaFicha();
        long fichaB = NovaFicha();
        long fichaC = NovaFicha();

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO Membros (nome, cpf, telefone, endereco, data_cadastro,
                                 id_treino_a, id_treino_b, id_treino_c)
            VALUES ($nome, $cpf, '48999999999', 'Rua A, 1', $data, $a, $b, $c);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$nome", nome);
        insert.Parameters.AddWithValue("$cpf", cpf);
        insert.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        insert.Parameters.AddWithValue("$a", fichaA);
        insert.Parameters.AddWithValue("$b", fichaB);
        insert.Parameters.AddWithValue("$c", fichaC);
        long id = (long)insert.ExecuteScalar()!;

        tx.Commit();
        return new ClienteComFichas(id, fichaA, fichaB, fichaC);
    }

    public long AddHistoricoTreino(long idMembro, long idTreino, string dia = "2026-01-01")
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Historico_Treinos (id_membro, id_treino, dia)
            VALUES ($membro, $treino, $dia);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$membro", idMembro);
        cmd.Parameters.AddWithValue("$treino", idTreino);
        cmd.Parameters.AddWithValue("$dia", dia);
        return (long)cmd.ExecuteScalar()!;
    }
    
    public void AddAssinatura(long idMembro, string dataFinal)
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Validade_Assinaturas (id_membro, data_final) VALUES ($id, $data)";
        cmd.Parameters.AddWithValue("$id", idMembro);
        cmd.Parameters.AddWithValue("$data", dataFinal);
        cmd.ExecuteNonQuery();
    }
    
    public long AddExercicio(string nome = "Supino", string? descricao = null)
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Exercicios (nome, descricao) VALUES ($nome, $descricao);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("$nome", nome);
        cmd.Parameters.AddWithValue("$descricao", (object?)descricao ?? DBNull.Value);
        return (long)cmd.ExecuteScalar()!;
    }
    
    public long AddSet(long idExercicio, int sets = 3, int repeticoes = 10)
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Sets (id_exercicio, sets, repeticoes) VALUES ($e, $s, $r);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("$e", idExercicio);
        cmd.Parameters.AddWithValue("$s", sets);
        cmd.Parameters.AddWithValue("$r", repeticoes);
        return (long)cmd.ExecuteScalar()!;
    }
    
    private static long _seqFunc;
    
    public long AddFuncionario(string nome = "Maria", string cargo = "Recepcionista", string? login = null)
    {
        login ??= $"func{Interlocked.Increment(ref _seqFunc)}";
    
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Funcionarios (nome, cargo, login, senha_hash, senha_salt)
            VALUES ($nome, $cargo, $login, 'hash', 'salt');
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$nome", nome);
        cmd.Parameters.AddWithValue("$cargo", cargo);
        cmd.Parameters.AddWithValue("$login", login);
        return (long)cmd.ExecuteScalar()!;
    }
    
    public long AddPagamento(long idMembro, long idFuncionario, decimal valor = 100m,
                             string status = "pago", string data = "2026-11-25 10:00:00")
    {
        using var conn = AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Pagamentos (id_membro, id_funcionario, valor, data_pagamento, status)
            VALUES ($m, $f, $valor, $data, $status);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$m", idMembro);
        cmd.Parameters.AddWithValue("$f", idFuncionario);
        cmd.Parameters.AddWithValue("$valor", valor);
        cmd.Parameters.AddWithValue("$data", data);
        cmd.Parameters.AddWithValue("$status", status);
        return (long)cmd.ExecuteScalar()!;
    }
}

public record ClienteComFichas(long Id, long FichaA, long FichaB, long FichaC);