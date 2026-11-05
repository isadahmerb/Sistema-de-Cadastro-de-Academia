"use client";

import { useState } from "react";

type Exercicio = { id: number; nome: string };
type ItemTreino = { id: number; repeticoes: string; series: string; obs: string };

const normalize = (s: string) =>
  s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();

// Dados de referência: depois troca pela chamada ao backend
const catalogoInicial: Exercicio[] = [
  { id: 1, nome: "Supino reto" },
  { id: 2, nome: "Crucifixo" },
  { id: 3, nome: "Tríceps testa" },
  { id: 4, nome: "Puxada frontal" },
  { id: 5, nome: "Remada baixa" },
  { id: 6, nome: "Rosca direta" },
  { id: 7, nome: "Agachamento" },
  { id: 8, nome: "Leg press" },
  { id: 9, nome: "Cadeira extensora" },
  { id: 10, nome: "Panturrilha" },
];

export default function EditarTreinoPage() {
  const [catalogo, setCatalogo] = useState(catalogoInicial);
  const [busca, setBusca] = useState("");
  const [treinoAtual, setTreinoAtual] = useState("A");
  const [treinos, setTreinos] = useState<Record<string, ItemTreino[]>>({
    A: [
      { id: 1, repeticoes: "8 - 12", series: "3", obs: "" },
      { id: 2, repeticoes: "10 - 12", series: "3", obs: "" },
    ],
    B: [],
    C: [],
  });
  const [aberto, setAberto] = useState<number | null>(null);
  const [salvo, setSalvo] = useState(false);

  const itens = treinos[treinoAtual];
  const filtrados = catalogo.filter((e) =>
    normalize(e.nome).includes(normalize(busca))
  );
  const marcado = (id: number) => itens.some((i) => i.id === id);
  const nomeDe = (id: number) => catalogo.find((e) => e.id === id)?.nome ?? "";

  const alternar = (id: number) => {
    setTreinos((prev) => {
      const atual = prev[treinoAtual];
      const novo = atual.some((i) => i.id === id)
        ? atual.filter((i) => i.id !== id)
        : [...atual, { id, repeticoes: "", series: "", obs: "" }];
      return { ...prev, [treinoAtual]: novo };
    });
  };

  const editar = (id: number, campo: "repeticoes" | "series" | "obs", valor: string) => {
    setTreinos((prev) => ({
      ...prev,
      [treinoAtual]: prev[treinoAtual].map((i) =>
        i.id === id ? { ...i, [campo]: valor } : i
      ),
    }));
  };

  const adicionarExercicio = () => {
    const nome = window.prompt("Nome do exercício:")?.trim();
    if (!nome) return;
    // TODO: integrar com o backend (POST /exercicios)
    setCatalogo((prev) => [...prev, { id: Date.now(), nome }]);
  };

  const salvar = () => {
    // TODO: integrar com o backend (PUT /alunos/:id/treinos/:treino)
    console.log("Salvar treino", treinoAtual, itens);
    setSalvo(true);
    setTimeout(() => setSalvo(false), 3000);
  };

  return (
    <main style={{ margin: "10vh auto" }}>
      <header className="text-center">
        <h1 className="text-2xl font-bold">ACADEMIA UNISUL</h1>
        <p className="font-bold">Perfil ADM</p>
      </header>

      <div className="mx-auto mt-10 grid max-w-5xl grid-cols-1 gap-6 md:grid-cols-2">
        {/* Esquerda: lista de exercícios */}
        <div className="flex flex-col gap-2">
          <section className="flex h-[32rem] flex-col rounded-lg border border-black bg-white">
            <header className="flex items-center justify-between gap-4 p-3">
              <h2 className="text-lg font-bold">Lista exercícios</h2>
              <input
                type="search"
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                placeholder="Pesquisar nome"
                className="w-40 border-b border-black text-sm outline-none"
              />
            </header>

            <ul className="flex-1 overflow-y-auto border-t border-black">
              {filtrados.length === 0 && (
                <li className="p-3 text-sm text-gray-500">Nenhum resultado</li>
              )}
              {filtrados.map((e) => (
                <li key={e.id}>
                  <label
                    className={`flex h-10 w-full cursor-pointer items-center gap-3 border-b border-black px-3 ${
                      marcado(e.id)
                        ? "bg-blue-300"
                        : "odd:bg-white even:bg-gray-300 hover:bg-blue-100"
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={marcado(e.id)}
                      onChange={() => alternar(e.id)}
                      className="h-4 w-4"
                    />
                    <span>{e.nome}</span>
                  </label>
                </li>
              ))}
            </ul>
          </section>

          <button
            type="button"
            onClick={adicionarExercicio}
            className="rounded border border-black p-2 text-left font-bold hover:bg-gray-100"
          >
            + Adicionar exercício
          </button>
        </div>

        {/* Direita: treino sendo editado */}
        <section className="flex h-[32rem] flex-col rounded-lg border border-black bg-white">
          <header className="flex items-center justify-between border-b border-black p-3">
            <select
              value={treinoAtual}
              onChange={(e) => {
                setTreinoAtual(e.target.value);
                setAberto(null);
              }}
              className="border border-black px-2 py-1 font-bold"
            >
              <option value="A">Treino A</option>
              <option value="B">Treino B</option>
              <option value="C">Treino C</option>
            </select>

            <button
              type="button"
              onClick={salvar}
              className="rounded border border-black bg-blue-300 px-4 py-1 text-sm font-bold hover:bg-blue-400"
            >
              Salvar
            </button>
          </header>

          <div className="flex-1 overflow-y-auto p-3">
            <h3 className="mb-1 font-bold">Exercícios</h3>

            {itens.length === 0 && (
              <p className="text-sm text-gray-500">
                Marque exercícios na lista ao lado para montar o treino.
              </p>
            )}

            <div className="space-y-1">
              {itens.map((item) => {
                const isOpen = aberto === item.id;
                return (
                  <div key={item.id} className="border border-black">
                    <button
                      type="button"
                      onClick={() => setAberto(isOpen ? null : item.id)}
                      className="flex w-full items-center justify-between bg-blue-100 px-3 py-1 font-bold"
                    >
                      <span>{nomeDe(item.id)}</span>
                      <span className="text-xs">{isOpen ? "^" : "v"}</span>
                    </button>

                    {isOpen && (
                      <div className="space-y-2 bg-white p-3 text-sm">
                        <label className="flex items-center gap-2">
                          Repetições:
                          <input
                            type="text"
                            value={item.repeticoes}
                            onChange={(e) => editar(item.id, "repeticoes", e.target.value)}
                            placeholder="8 - 12"
                            className="w-24 border-b border-black outline-none"
                          />
                        </label>
                        <label className="flex items-center gap-2">
                          Séries:
                          <input
                            type="number"
                            min={1}
                            value={item.series}
                            onChange={(e) => editar(item.id, "series", e.target.value)}
                            className="w-16 border-b border-black outline-none"
                          />
                        </label>
                        <label className="flex items-center gap-2">
                          Obs:
                          <input
                            type="text"
                            value={item.obs}
                            onChange={(e) => editar(item.id, "obs", e.target.value)}
                            className="flex-1 border-b border-black outline-none"
                          />
                        </label>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            {salvo && (
              <p className="mt-3 text-center text-sm font-bold text-green-700">
                Treino {treinoAtual} salvo!
              </p>
            )}
          </div>
        </section>
      </div>
    </main>
  );
}