using System.Net.Http.Json;

namespace backend.Tests;

public class ListagemClientesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    private readonly HttpClient _http;

    public ListagemClientesTests(ApiFactory f)
    {
        _f = f;
        _http = f.CreateClient();
        _f.Limpar();   // banco zerado antes de cada teste
    }

    private Task<List<Dictionary<string, object>>?> Listar() =>
        _http.GetFromJsonAsync<List<Dictionary<string, object>>>("/clientes");

    [Fact]
    public async Task Lista_sem_membros_retorna_vazio()
    {
        var lista = await Listar();

        Assert.Empty(lista!);
    }

    [Fact]
    public async Task Lista_com_um_membro_retorna_somente_ele()
    {
        _f.AddCliente("Ana");

        var lista = await Listar();

        var unico = Assert.Single(lista!);
        Assert.Equal("Ana", unico["nome"].ToString());
    }

    [Fact]
    public async Task Lista_com_varios_membros_retorna_todos()
    {
        _f.AddCliente("Ana");
        _f.AddCliente("Bia");
        _f.AddCliente("Caio");

        var lista = await Listar();

        Assert.Equal(3, lista!.Count);
    }
}
