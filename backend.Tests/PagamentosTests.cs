using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class PagamentosTests : IClassFixture<ApiFactory>
{
    private record Pagamento(long id, long id_membro, long id_funcionario, decimal valor, string data_pagamento, string status);
    private record IdResposta(long id);

    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public PagamentosTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> Post(long membro, long funcionario, decimal valor, string status) =>
        _http.PostAsJsonAsync("/pagamentos", new { id_membro = membro, id_funcionario = funcionario, valor, status });

    private Task<List<Pagamento>?> Listar() =>
        _http.GetFromJsonAsync<List<Pagamento>>("/pagamentos");

    private Task<List<Pagamento>?> ListarDoCliente(long membro) =>
        _http.GetFromJsonAsync<List<Pagamento>>($"/pagamentos/{membro}");

    // ---------- POST ----------

    [Fact]
    public async Task Post_valido_retorna_201_com_id_e_grava_o_pagamento()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();

        var resp = await Post(ana, func, 99.90m, "pago");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.True((await resp.Content.ReadFromJsonAsync<IdResposta>())!.id > 0);

        var p = Assert.Single((await Listar())!);
        Assert.Equal(ana, p.id_membro);
        Assert.Equal(func, p.id_funcionario);
        Assert.Equal(99.90m, p.valor);
        Assert.Equal("pago", p.status);
    }

    [Fact]
    public async Task Post_registra_a_data_de_hoje()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();

        await Post(ana, func, 50m, "pago");

        var p = Assert.Single((await Listar())!);
        Assert.StartsWith(DateTime.Now.ToString("yyyy-MM-dd"), p.data_pagamento);
    }

    [Fact]
    public async Task Post_com_valor_zero_e_aceito()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();

        var resp = await Post(ana, func, 0m, "isento");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Post_com_valor_negativo_retorna_400()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();

        var resp = await Post(ana, func, -1m, "pago");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Empty((await Listar())!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_com_status_vazio_retorna_400(string status)
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();

        var resp = await Post(ana, func, 50m, status);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_com_membro_inexistente_retorna_404_citando_o_cliente()
    {
        long func = _f.AddFuncionario();

        var resp = await Post(999, func, 50m, "pago");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Contains("cliente", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_com_funcionario_inexistente_retorna_404_citando_o_funcionario()
    {
        long ana = _f.AddCliente("Ana");

        var resp = await Post(ana, 999, 50m, "pago");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Contains("funcionário", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_valida_o_membro_antes_do_funcionario()
    {
        var resp = await Post(999, 999, 50m, "pago");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Contains("cliente", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_valor_negativo_tem_precedencia_sobre_membro_inexistente()
    {
        var resp = await Post(999, 999, -5m, "pago");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------- GET lista ----------

    [Fact]
    public async Task Lista_vazia_retorna_200_com_array_vazio()
    {
        var resp = await _http.GetAsync("/pagamentos");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Pagamento>>())!);
    }

    [Fact]
    public async Task Lista_ordena_por_data_decrescente_e_depois_por_id_decrescente()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();
        long antigo = _f.AddPagamento(ana, func, data: "2026-11-01 10:00:00");
        long tarde = _f.AddPagamento(ana, func, data: "2026-11-03 18:00:00");
        long manhaA = _f.AddPagamento(ana, func, data: "2026-11-03 09:00:00");
        long manhaB = _f.AddPagamento(ana, func, data: "2026-11-03 09:00:00");

        var lista = await Listar();

        Assert.Equal(new[] { tarde, manhaB, manhaA, antigo }, lista!.Select(p => p.id).ToArray());
    }

    // ---------- GET por cliente ----------

    [Fact]
    public async Task Pagamentos_de_cliente_inexistente_retorna_404()
    {
        var resp = await _http.GetAsync("/pagamentos/999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Pagamentos_de_cliente_sem_pagamentos_retorna_lista_vazia()
    {
        long ana = _f.AddCliente("Ana");

        var resp = await _http.GetAsync($"/pagamentos/{ana}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty((await resp.Content.ReadFromJsonAsync<List<Pagamento>>())!);
    }

    [Fact]
    public async Task Pagamentos_do_cliente_nao_incluem_os_de_outros()
    {
        long ana = _f.AddCliente("Ana");
        long bia = _f.AddCliente("Bia");
        long func = _f.AddFuncionario();
        _f.AddPagamento(ana, func);
        _f.AddPagamento(ana, func);
        _f.AddPagamento(bia, func);

        var lista = await ListarDoCliente(ana);

        Assert.Equal(2, lista!.Count);
        Assert.All(lista, p => Assert.Equal(ana, p.id_membro));
    }

    [Fact]
    public async Task Pagamentos_do_cliente_vem_do_mais_recente_para_o_mais_antigo()
    {
        long ana = _f.AddCliente("Ana");
        long func = _f.AddFuncionario();
        long p1 = _f.AddPagamento(ana, func, data: "2026-10-01 10:00:00");
        long p2 = _f.AddPagamento(ana, func, data: "2026-11-01 10:00:00");

        var lista = await ListarDoCliente(ana);

        Assert.Equal(new[] { p2, p1 }, lista!.Select(p => p.id).ToArray());
    }
}
