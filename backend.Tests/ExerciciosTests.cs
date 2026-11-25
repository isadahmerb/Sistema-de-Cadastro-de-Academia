using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class ExerciciosTests : IClassFixture<ApiFactory>
{
    private record Exercicio(long id, string nome, string? descricao);

    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public ExerciciosTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> Post(string nome, string? descricao = null) =>
        _http.PostAsJsonAsync("/exercicios", new { nome, descricao });

    private Task<HttpResponseMessage> Put(long id, string nome, string? descricao = null) =>
        _http.PutAsJsonAsync($"/exercicios/{id}", new { nome, descricao });

    private Task<Exercicio?> Buscar(long id) =>
        _http.GetFromJsonAsync<Exercicio>($"/exercicios/{id}");

    private Task<HttpResponseMessage> PostSet(long idExercicio, int sets, int repeticoes) =>
        _http.PostAsJsonAsync("/sets", new { id_exercicio = idExercicio, sets, repeticoes });

    // ---------- POST /exercicios ----------

    [Fact]
    public async Task Post_com_nome_e_descricao_retorna_201_e_grava()
    {
        var resp = await Post("Supino", "Peito");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var lista = await _http.GetFromJsonAsync<List<Exercicio>>("/exercicios");
        var e = Assert.Single(lista!);
        Assert.Equal("Supino", e.nome);
        Assert.Equal("Peito", e.descricao);
    }

    [Fact]
    public async Task Post_so_com_nome_retorna_201_com_descricao_nula()
    {
        var resp = await Post("Agachamento");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var e = Assert.Single((await _http.GetFromJsonAsync<List<Exercicio>>("/exercicios"))!);
        Assert.Null(e.descricao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_nome_vazio_retorna_400(string nome)
    {
        var resp = await Post(nome);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- GET lista ----------

    [Fact]
    public async Task Lista_vazia_retorna_200_com_array_vazio()
    {
        var resp = await _http.GetAsync("/exercicios");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Exercicio>>())!);
    }

    [Fact]
    public async Task Lista_ordena_por_nome()
    {
        _f.AddExercicio("Supino");
        _f.AddExercicio("Agachamento");
        _f.AddExercicio("Remada");

        var lista = await _http.GetFromJsonAsync<List<Exercicio>>("/exercicios");

        Assert.Equal(new[] { "Agachamento", "Remada", "Supino" }, lista!.Select(e => e.nome).ToArray());
    }

    // ---------- GET por id ----------

    [Fact]
    public async Task Get_por_id_com_descricao_retorna_o_exercicio()
    {
        long id = _f.AddExercicio("Supino", "Peito");

        var e = await Buscar(id);

        Assert.Equal(new Exercicio(id, "Supino", "Peito"), e);
    }

    [Fact]
    public async Task Get_por_id_sem_descricao_retorna_descricao_nula()
    {
        long id = _f.AddExercicio("Supino");

        var e = await Buscar(id);

        Assert.Null(e!.descricao);
    }

    [Fact]
    public async Task Get_por_id_inexistente_retorna_404()
    {
        var resp = await _http.GetAsync("/exercicios/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Put_valido_retorna_204_e_atualiza()
    {
        long id = _f.AddExercicio("Supino", "Peito");

        var resp = await Put(id, "Supino inclinado", "Peito superior");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(new Exercicio(id, "Supino inclinado", "Peito superior"), await Buscar(id));
    }

    [Fact]
    public async Task Put_sem_descricao_limpa_a_descricao()
    {
        long id = _f.AddExercicio("Supino", "Peito");

        await Put(id, "Supino");

        Assert.Null((await Buscar(id))!.descricao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_nome_vazio_retorna_400_e_nao_altera(string nome)
    {
        long id = _f.AddExercicio("Supino");

        var resp = await Put(id, nome);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("Supino", (await Buscar(id))!.nome);
    }

    [Fact]
    public async Task Put_id_inexistente_retorna_404()
    {
        var resp = await Put(999, "Supino");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Put_nome_vazio_tem_precedencia_sobre_id_inexistente()
    {
        var resp = await Put(999, "");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_sem_sets_retorna_204_e_remove()
    {
        long id = _f.AddExercicio("Supino");

        var resp = await _http.DeleteAsync($"/exercicios/{id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/exercicios/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_de_exercicio_usado_por_set_retorna_409_e_mantem()
    {
        long id = _f.AddExercicio("Supino");
        _f.AddSet(id);

        var resp = await _http.DeleteAsync($"/exercicios/{id}");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.NotNull(await Buscar(id));
    }

    [Fact]
    public async Task Delete_inexistente_retorna_404()
    {
        var resp = await _http.DeleteAsync("/exercicios/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- POST /sets ----------

    [Fact]
    public async Task Post_set_valido_retorna_201()
    {
        long ex = _f.AddExercicio("Supino");

        var resp = await PostSet(ex, 4, 12);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, 0)]
    [InlineData(-1, 10)]
    [InlineData(3, -5)]
    public async Task Post_set_com_valores_nao_positivos_retorna_400(int sets, int repeticoes)
    {
        long ex = _f.AddExercicio("Supino");

        var resp = await PostSet(ex, sets, repeticoes);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_set_com_exercicio_inexistente_retorna_400()
    {
        var resp = await PostSet(999, 3, 10);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
