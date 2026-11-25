using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class HistoricoTreinosTests : IClassFixture<ApiFactory>
{
    private record Registro(long id, long id_membro, long id_treino, string dia);

    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public HistoricoTreinosTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> Post(long membro, long treino, string? dia) =>
        _http.PostAsJsonAsync("/historico-treinos", new { id_membro = membro, id_treino = treino, dia });

    private Task<HttpResponseMessage> Put(long id, long treino, string dia) =>
        _http.PutAsJsonAsync($"/historico-treinos/{id}", new { id_treino = treino, dia });

    private Task<List<Registro>?> Listar(string query = "") =>
        _http.GetFromJsonAsync<List<Registro>>($"/historico-treinos{query}");

    private Task<Registro?> Buscar(long id) =>
        _http.GetFromJsonAsync<Registro>($"/historico-treinos/{id}");

    private static long Ficha(ClienteComFichas c, int i) => new[] { c.FichaA, c.FichaB, c.FichaC }[i];

    // ---------- POST ----------

    [Fact]
    public async Task Post_valido_retorna_201_e_grava_o_registro()
    {
        var ana = _f.AddClienteComFichas();

        var resp = await Post(ana.Id, ana.FichaA, "2026-11-20");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var lista = await Listar();
        var r = Assert.Single(lista!);
        Assert.Equal(ana.Id, r.id_membro);
        Assert.Equal(ana.FichaA, r.id_treino);
        Assert.Equal("2026-11-20", r.dia);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Post_aceita_as_fichas_A_B_e_C_do_membro(int ficha)
    {
        var ana = _f.AddClienteComFichas();

        var resp = await Post(ana.Id, Ficha(ana, ficha), "2026-11-20");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Post_sem_dia_usa_a_data_de_hoje()
    {
        var ana = _f.AddClienteComFichas();

        var resp = await Post(ana.Id, ana.FichaA, null);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var r = Assert.Single((await Listar())!);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), r.dia);
    }

    [Theory]
    [InlineData("25/11/2026")]
    [InlineData("")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    public async Task Post_dia_invalido_retorna_400(string dia)
    {
        var ana = _f.AddClienteComFichas();

        var resp = await Post(ana.Id, ana.FichaA, dia);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_membro_inexistente_retorna_404()
    {
        var resp = await Post(999, 1, "2026-11-20");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_ficha_de_outro_membro_retorna_400()
    {
        var ana = _f.AddClienteComFichas("Ana");
        var bia = _f.AddClienteComFichas("Bia");

        var resp = await Post(ana.Id, bia.FichaA, "2026-11-20");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_treino_inexistente_retorna_400()
    {
        var ana = _f.AddClienteComFichas();

        var resp = await Post(ana.Id, 9999, "2026-11-20");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_dia_invalido_tem_precedencia_sobre_membro_inexistente()
    {
        var resp = await Post(999, 1, "lixo");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Put_valido_retorna_204_e_atualiza_o_registro()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA, "2026-11-01");

        var resp = await Put(id, ana.FichaA, "2026-11-25");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal("2026-11-25", (await Buscar(id))!.dia);
    }

    [Fact]
    public async Task Put_pode_trocar_de_ficha_A_para_B()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA);

        var resp = await Put(id, ana.FichaB, "2026-11-25");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(ana.FichaB, (await Buscar(id))!.id_treino);
    }

    [Fact]
    public async Task Put_dia_invalido_retorna_400()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA);

        var resp = await Put(id, ana.FichaA, "25/11/2026");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Put_id_inexistente_retorna_404()
    {
        var resp = await Put(999, 1, "2026-11-25");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Put_ficha_de_outro_membro_retorna_400()
    {
        var ana = _f.AddClienteComFichas("Ana");
        var bia = _f.AddClienteComFichas("Bia");
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA);

        var resp = await Put(id, bia.FichaA, "2026-11-25");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Put_dia_invalido_tem_precedencia_sobre_id_inexistente()
    {
        var resp = await Put(999, 1, "lixo");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- GET lista ----------

    [Fact]
    public async Task Lista_vazia_retorna_200_com_array_vazio()
    {
        var resp = await _http.GetAsync("/historico-treinos");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Registro>>())!);
    }

    [Fact]
    public async Task Lista_ordena_por_dia_decrescente_e_depois_por_id_decrescente()
    {
        var ana = _f.AddClienteComFichas();
        long id1 = _f.AddHistoricoTreino(ana.Id, ana.FichaA, "2026-11-01");
        long id2 = _f.AddHistoricoTreino(ana.Id, ana.FichaA, "2026-11-03");
        long id3 = _f.AddHistoricoTreino(ana.Id, ana.FichaA, "2026-11-03");

        var lista = await Listar();

        Assert.Equal(new[] { id3, id2, id1 }, lista!.Select(r => r.id).ToArray());
    }

    [Fact]
    public async Task Filtro_por_membro_retorna_so_os_registros_dele()
    {
        var ana = _f.AddClienteComFichas("Ana");
        var bia = _f.AddClienteComFichas("Bia");
        _f.AddHistoricoTreino(ana.Id, ana.FichaA);
        _f.AddHistoricoTreino(ana.Id, ana.FichaB);
        _f.AddHistoricoTreino(bia.Id, bia.FichaA);

        var lista = await Listar($"?id_membro={ana.Id}");

        Assert.Equal(2, lista!.Count);
        Assert.All(lista, r => Assert.Equal(ana.Id, r.id_membro));
    }

    [Fact]
    public async Task Filtro_por_membro_sem_registros_retorna_vazio()
    {
        var ana = _f.AddClienteComFichas("Ana");
        var bia = _f.AddClienteComFichas("Bia");
        _f.AddHistoricoTreino(bia.Id, bia.FichaA);

        var lista = await Listar($"?id_membro={ana.Id}");

        Assert.Empty(lista!);
    }

    [Fact]
    public async Task Filtro_por_membro_inexistente_retorna_vazio_e_nao_404()
    {
        var resp = await _http.GetAsync("/historico-treinos?id_membro=999");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Registro>>())!);
    }

    // ---------- GET por id ----------

    [Fact]
    public async Task Get_por_id_existente_retorna_o_registro()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaC, "2026-11-10");

        var r = await Buscar(id);

        Assert.Equal(new Registro(id, ana.Id, ana.FichaC, "2026-11-10"), r);
    }

    [Fact]
    public async Task Get_por_id_inexistente_retorna_404()
    {
        var resp = await _http.GetAsync("/historico-treinos/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_existente_retorna_204_e_remove()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA);

        var resp = await _http.DeleteAsync($"/historico-treinos/{id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Empty((await Listar())!);
    }

    [Fact]
    public async Task Delete_inexistente_retorna_404()
    {
        var resp = await _http.DeleteAsync("/historico-treinos/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_duas_vezes_retorna_204_e_depois_404()
    {
        var ana = _f.AddClienteComFichas();
        long id = _f.AddHistoricoTreino(ana.Id, ana.FichaA);

        var primeira = await _http.DeleteAsync($"/historico-treinos/{id}");
        var segunda = await _http.DeleteAsync($"/historico-treinos/{id}");

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
    }
}
