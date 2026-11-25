namespace backend.Tests;

public class BancoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public BancoTests(ApiFactory f) => _f = f;

    [Fact]
    public void Banco_existe_e_tem_as_tabelas()
    {
        Assert.True(File.Exists(_f.DbPath));

        using var conn = _f.AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";

        Assert.Equal(12L, (long)cmd.ExecuteScalar()!);
    }
}
