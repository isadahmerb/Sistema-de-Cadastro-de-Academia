using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class AssinaturasTests : IClassFixture<ApiFactory>
{
    private record Assinatura(long id_membro, string data_final, bool ativa);

    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public AssinaturasTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();
    }

    // ---------- helpers ----------

    private static string Dia(int deslocamento) =>
        DateTime.Now.AddDays(deslocamento).ToString("yyyy-MM-dd");

    private Task<HttpResponseMessage> Post(long membro, string dataFinal) =>
        _http.PostAsJsonAsync("/assinaturas", new { id_membro = membro, data_final = dataFinal });

    private Task<HttpResponseMessage> Put(long membro, string dataFinal) =>
        _http.PutAsJsonAsync($"/assinaturas/{membro}", new { data_final = dataFinal });

    private Task<Assinatura?> Buscar(long membro) =>
        _http.GetFromJsonAsync<Assinatura>($"/assinaturas/{membro}");

    private Task<List<Assinatura>?> Listar() =>
        _http.GetFromJsonAsync<List<Assinatura>>("/assinaturas");

    // ---------- POST ----------

    [Fact]
    public async Task Post_valido_retorna_201_e_grava_a_assinatura()
    {
        long ana = _f.AddCliente("Ana");

        var resp = await Post(ana, "2026-12-31");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal("2026-12-31", (await Buscar(ana))!.data_final);
    }

    [Theory]
    [InlineData("25/11/2026")]
    [InlineData("")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    public async Task Post_data_invalida_retorna_400(string data)
    {
        long ana = _f.AddCliente("Ana");

        var resp = await Post(ana, data);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_membro_inexistente_retorna_404()
    {
        var resp = await Post(999, "2026-12-31");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_segunda_assinatura_do_mesmo_membro_retorna_409_e_mantem_a_original()
    {
        long ana = _f.AddCliente("Ana");
        await Post(ana, "2026-12-31");

        var resp = await Post(ana, "2027-06-30");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("2026-12-31", (await Buscar(ana))!.data_final);
    }

    [Fact]
    public async Task Post_data_invalida_tem_precedencia_sobre_membro_inexistente()
    {
        var resp = await Post(999, "lixo");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- GET lista ----------

    [Fact]
    public async Task Lista_vazia_retorna_200_com_array_vazio()
    {
        var resp = await _http.GetAsync("/assinaturas");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Assinatura>>())!);
    }

    [Fact]
    public async Task Lista_ordena_por_data_final_crescente()
    {
        long ana = _f.AddCliente("Ana");
        long bia = _f.AddCliente("Bia");
        long caio = _f.AddCliente("Caio");
        _f.AddAssinatura(ana, "2026-12-31");
        _f.AddAssinatura(bia, "2026-01-15");
        _f.AddAssinatura(caio, "2026-06-30");

        var lista = await Listar();

        Assert.Equal(new[] { bia, caio, ana }, lista!.Select(a => a.id_membro).ToArray());
    }

    // ---------- GET por membro ----------

    [Theory]
    [InlineData(1, true)]    // vence amanhã
    [InlineData(0, true)]    // vence hoje: vale até o fim do dia
    [InlineData(-1, false)]  // venceu ontem
    public async Task Get_calcula_o_campo_ativa_pela_data_final(int deslocamentoDias, bool esperadoAtiva)
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, Dia(deslocamentoDias));

        var a = await Buscar(ana);

        Assert.Equal(esperadoAtiva, a!.ativa);
    }

    [Fact]
    public async Task Get_membro_sem_assinatura_retorna_404()
    {
        long ana = _f.AddCliente("Ana");

        var resp = await _http.GetAsync($"/assinaturas/{ana}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Put_valido_retorna_204_e_atualiza_a_data()
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, "2026-12-31");

        var resp = await Put(ana, "2027-06-30");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal("2027-06-30", (await Buscar(ana))!.data_final);
    }

    [Fact]
    public async Task Put_reativa_uma_assinatura_vencida()
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, Dia(-10));
        Assert.False((await Buscar(ana))!.ativa);

        await Put(ana, Dia(30));

        Assert.True((await Buscar(ana))!.ativa);
    }

    [Fact]
    public async Task Put_data_invalida_retorna_400()
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, "2026-12-31");

        var resp = await Put(ana, "25/11/2026");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("2026-12-31", (await Buscar(ana))!.data_final);
    }

    [Fact]
    public async Task Put_membro_sem_assinatura_retorna_404()
    {
        long ana = _f.AddCliente("Ana");

        var resp = await Put(ana, "2026-12-31");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Put_data_invalida_tem_precedencia_sobre_assinatura_inexistente()
    {
        var resp = await Put(999, "lixo");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_existente_retorna_204_e_remove()
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, "2026-12-31");

        var resp = await _http.DeleteAsync($"/assinaturas/{ana}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/assinaturas/{ana}")).StatusCode);
    }

    [Fact]
    public async Task Delete_inexistente_retorna_404()
    {
        var resp = await _http.DeleteAsync("/assinaturas/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_duas_vezes_retorna_204_e_depois_404()
    {
        long ana = _f.AddCliente("Ana");
        _f.AddAssinatura(ana, "2026-12-31");

        var primeira = await _http.DeleteAsync($"/assinaturas/{ana}");
        var segunda = await _http.DeleteAsync($"/assinaturas/{ana}");

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
    }
}
