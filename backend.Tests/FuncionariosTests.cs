using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace backend.Tests;

public class FuncionariosTests : IClassFixture<ApiFactory>
{
    private record IdResposta(long id);
    private record TokenResposta(string token);

    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public FuncionariosTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> CriarFuncionario(
        string login = "maria", string senha = "senha123", string nome = "Maria", string cargo = "Recepcionista") =>
        _http.PostAsJsonAsync("/employee", new { name = nome, role = cargo, login, password = senha });

    private Task<HttpResponseMessage> Login(string login, string senha) =>
        _http.PostAsJsonAsync("/login", new { login, password = senha });

    private long ContarSessoes()
    {
        using var conn = _f.AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Current_sessions";
        return (long)cmd.ExecuteScalar()!;
    }

    private (string hash, string salt) LerCredenciais(string login)
    {
        using var conn = _f.AbrirConexao();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT senha_hash, senha_salt FROM Funcionarios WHERE login = $login";
        cmd.Parameters.AddWithValue("$login", login);
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.GetString(0), r.GetString(1));
    }

    // ---------- POST /employee ----------

    [Fact]
    public async Task Cadastrar_funcionario_valido_retorna_201_com_id()
    {
        var resp = await CriarFuncionario();

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var corpo = await resp.Content.ReadFromJsonAsync<IdResposta>();
        Assert.True(corpo!.id > 0);
    }

    [Fact]
    public async Task Cadastrar_com_login_repetido_retorna_409()
    {
        await CriarFuncionario("maria");

        var resp = await CriarFuncionario("maria", nome: "Outra Maria");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Cadastrar_com_logins_diferentes_funciona()
    {
        var a = await CriarFuncionario("maria");
        var b = await CriarFuncionario("joao", nome: "João");

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
    }

    [Fact]
    public async Task Senha_nao_e_guardada_em_texto_puro()
    {
        await CriarFuncionario("maria", "senha123");

        var (hash, salt) = LerCredenciais("maria");

        Assert.NotEqual("senha123", hash);
        Assert.DoesNotContain("senha123", hash);
        Assert.False(string.IsNullOrEmpty(salt));
    }

    [Fact]
    public async Task Mesma_senha_gera_hash_e_salt_diferentes_para_cada_funcionario()
    {
        await CriarFuncionario("maria", "igual");
        await CriarFuncionario("joao", "igual", nome: "João");

        var a = LerCredenciais("maria");
        var b = LerCredenciais("joao");

        Assert.NotEqual(a.salt, b.salt);
        Assert.NotEqual(a.hash, b.hash);
    }

    // ---------- POST /login ----------

    [Fact]
    public async Task Login_correto_retorna_200_com_token_de_32_caracteres_hex()
    {
        await CriarFuncionario("maria", "senha123");

        var resp = await Login("maria", "senha123");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var corpo = await resp.Content.ReadFromJsonAsync<TokenResposta>();
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), corpo!.token);
    }

    [Fact]
    public async Task Login_correto_grava_a_sessao_no_banco()
    {
        await CriarFuncionario("maria", "senha123");

        await Login("maria", "senha123");

        Assert.Equal(1, ContarSessoes());
    }

    [Fact]
    public async Task Dois_logins_seguidos_geram_tokens_diferentes()
    {
        await CriarFuncionario("maria", "senha123");

        var t1 = await (await Login("maria", "senha123")).Content.ReadFromJsonAsync<TokenResposta>();
        var t2 = await (await Login("maria", "senha123")).Content.ReadFromJsonAsync<TokenResposta>();

        Assert.NotEqual(t1!.token, t2!.token);
        Assert.Equal(2, ContarSessoes());
    }

    [Fact]
    public async Task Login_com_senha_errada_retorna_401_e_nao_cria_sessao()
    {
        await CriarFuncionario("maria", "senha123");

        var resp = await Login("maria", "errada");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal(0, ContarSessoes());
    }

    [Fact]
    public async Task Senha_diferencia_maiusculas_de_minusculas()
    {
        await CriarFuncionario("maria", "Senha123");

        var resp = await Login("maria", "senha123");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_inexistente_retorna_401()
    {
        var resp = await Login("ninguem", "senha123");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Senha_so_com_espacos_e_tratada_como_tentativa_e_retorna_401()
    {
        await CriarFuncionario("maria", "senha123");

        var resp = await Login("maria", "   ");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Login_vazio_retorna_400(string login)
    {
        var resp = await Login(login, "senha123");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Senha_vazia_retorna_400()
    {
        await CriarFuncionario("maria", "senha123");

        var resp = await Login("maria", "");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
