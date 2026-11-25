"use client";

import { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";

type Exercicio = { id: number; nome: string };
type Item = {
  chave: string;
  idSet: number | null; 
  idExercicio: number | null;
  nome: string;
  series: string;
  repeticoes: string;
};
type Letra = "A" | "B" | "C";
type Fichas = Record<Letra, Item[]>;
type TreinoApi = {
  exercicios: {
    id_set: number;
    id_exercicio?: number;
    nome: string;
    sets: { sets: number; repeticoes: number };
  }[];
};

const LETRAS: Letra[] = ["A", "B", "C"];
const JSON_HEADERS = { "Content-Type": "application/json" };

const normalize = (s: string) =>
  s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();


const campo = (o: Record<string, unknown>, nome: string) =>
  o[Object.keys(o).find((k) => k.toLowerCase() === nome) ?? ""];

const numOuNull = (v: unknown) => (v == null ? null : Number(v));


const converter = (t: TreinoApi, catalogo: Exercicio[]): Item[] =>
  t.exercicios.map((e) => ({
    chave: `set${e.id_set}`,
    idSet: e.id_set,
    idExercicio:
      e.id_exercicio ??
      catalogo.find((c) => normalize(c.nome) === normalize(e.nome))?.id ??
      null,
    nome: e.nome,
    series: String(e.sets.sets),
    repeticoes: String(e.sets.repeticoes),
  }));

function EditarTreino() {
  const { id } = useParams<{ id: string }>();

  const [nomeAluno, setNomeAluno] = useState("");
  const [idsTreino, setIdsTreino] = useState<Record<Letra, number | null>>({
    A: null,
    B: null,
    C: null,
  });
  const [catalogo, setCatalogo] = useState<Exercicio[]>([]);
  const [original, setOriginal] = useState<Fichas>({ A: [], B: [], C: [] });
  const [fichas, setFichas] = useState<Fichas>({ A: [], B: [], C: [] });
  const [treinoAtual, setTreinoAtual] = useState<Letra>("A");
  const [busca, setBusca] = useState("");
  const [aberto, setAberto] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [falhou, setFalhou] = useState(false);
  const [erro, setErro] = useState("");
  const [salvando, setSalvando] = useState(false);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    async function carregar() {
      const [rCliente, rTreinos, rExercicios] = await Promise.all([
        fetch(`/api/clientes/${id}`),
        fetch(`/api/treinos/${id}`),
        fetch("/api/exercicios"),
      ]);
      if (!rCliente.ok || !rTreinos.ok || !rExercicios.ok) throw new Error();

      const cliente: Record<string, unknown> = await rCliente.json();
      const { treinos }: { treinos: TreinoApi[] } = await rTreinos.json();
      const exercicios: Exercicio[] = await rExercicios.json();

      const novas: Fichas = {
        A: converter(treinos[0], exercicios),
        B: converter(treinos[1], exercicios),
        C: converter(treinos[2], exercicios),
      };

      setNomeAluno(String(campo(cliente, "nome")));
      setIdsTreino({
        A: numOuNull(campo(cliente, "id_treino_a")),
        B: numOuNull(campo(cliente, "id_treino_b")),
        C: numOuNull(campo(cliente, "id_treino_c")),
      });
      setCatalogo(exercicios);
      setOriginal(novas);
      setFichas(novas);
    }

    carregar()
      .catch(() => setFalhou(true))
      .finally(() => setCarregando(false));
  }, [id]);

  const itens = fichas[treinoAtual];
  const marcado = (idEx: number) => itens.some((i) => i.idExercicio === idEx);
  const filtrados = catalogo.filter((e) =>
    normalize(e.nome).includes(normalize(busca))
  );

  const atualizarItens = (fn: (lista: Item[]) => Item[]) =>
    setFichas((p) => ({ ...p, [treinoAtual]: fn(p[treinoAtual]) }));

  const alternar = (e: Exercicio) =>
    atualizarItens((lista) =>
      lista.some((i) => i.idExercicio === e.id)
        ? lista.filter((i) => i.idExercicio !== e.id)
        : [
            ...lista,
            {
              chave: `novo${e.id}`,
              idSet: null,
              idExercicio: e.id,
              nome: e.nome,
              series: "3",
              repeticoes: "12",
            },
          ]
    );

  const editar = (chave: string, campoItem: "series" | "repeticoes", valor: string) =>
    atualizarItens((lista) =>
      lista.map((i) => (i.chave === chave ? { ...i, [campoItem]: valor } : i))
    );

  async function adicionarExercicio() {
    const nome = window.prompt("Nome do exercício:")?.trim();
    if (!nome) return;

    if (catalogo.some((e) => normalize(e.nome) === normalize(nome))) {
      setErro("Esse exercício já existe na lista");
      return;
    }

    setErro("");
    try {
      const r = await fetch("/api/exercicios", {
        method: "POST",
        headers: JSON_HEADERS,
        body: JSON.stringify({ nome, descricao: null }),
      });
      if (!r.ok) throw new Error();
      const { id: novoId } = await r.json();

      setCatalogo((prev) =>
        [...prev, { id: Number(novoId), nome }].sort((a, b) =>
          a.nome.localeCompare(b.nome)
        )
      );
    } catch {
      setErro("Não foi possível adicionar o exercício");
    }
  }

  async function salvar() {
    const idTreino = idsTreino[treinoAtual];
    if (idTreino === null) return;

    const invalido = itens.some(
      (i) =>
        !Number.isInteger(Number(i.series)) ||
        !Number.isInteger(Number(i.repeticoes)) ||
        Number(i.series) <= 0 ||
        Number(i.repeticoes) <= 0
    );
    if (invalido) {
      setErro("Séries e repetições devem ser números inteiros maiores que zero");
      return;
    }

    setErro("");
    setSalvando(true);
    try {
      const antigos = original[treinoAtual];

      for (const a of antigos) {
        const r = await fetch(`/api/treinos/${idTreino}/set/${a.idSet}`, {
          method: "DELETE",
        });
        if (!r.ok && r.status !== 404) throw new Error();
      }

      for (const item of itens) {
        let idSet = item.idSet;
        const antigo = antigos.find((a) => a.idSet !== null && a.idSet === item.idSet);
        const mudou =
          !antigo ||
          antigo.series !== item.series ||
          antigo.repeticoes !== item.repeticoes;

        if (idSet === null || mudou) {
          if (item.idExercicio === null) throw new Error();
          const r = await fetch("/api/sets", {
            method: "POST",
            headers: JSON_HEADERS,
            body: JSON.stringify({
              id_exercicio: item.idExercicio,
              sets: Number(item.series),
              repeticoes: Number(item.repeticoes),
            }),
          });
          if (!r.ok) throw new Error();
          idSet = (await r.json()).id;
        }

        const r2 = await fetch(`/api/treinos/${idTreino}/set/${idSet}`, {
          method: "POST",
        });
        if (!r2.ok) throw new Error();
      }

      const rT = await fetch(`/api/treinos/${id}`);
      if (!rT.ok) throw new Error();
      const { treinos }: { treinos: TreinoApi[] } = await rT.json();
      const novos = converter(treinos[LETRAS.indexOf(treinoAtual)], catalogo);
      setOriginal((p) => ({ ...p, [treinoAtual]: novos }));
      setFichas((p) => ({ ...p, [treinoAtual]: novos }));

      setSalvo(true);
      setTimeout(() => setSalvo(false), 3000);
    } catch {
      setErro("Não foi possível salvar. Recarregue a página e confira o treino");
    } finally {
      setSalvando(false);
    }
  }

  if (carregando) return <main className="mt-[10vh] text-center">Carregando...</main>;
  if (falhou)
    return (
      <main className="mt-[10vh] text-center font-bold text-red-600">
        Não foi possível carregar os dados do aluno
      </main>
    );

  return (
    <main style={{ margin: "10vh auto" }}>
      <header className="text-center">
        <Link href="/home" className="fixed top-4 left-4 z-50">Voltar</Link>
        <h1 className="text-2xl font-bold">ACADEMIA UNISUL</h1>
        <p className="font-bold">Perfil ADM</p>
        <p className="text-sm">Editando: {nomeAluno}</p>
      </header>

      {erro && <p className="mt-4 text-center text-sm font-bold text-red-600">{erro}</p>}

      <div className="mx-auto mt-10 grid max-w-5xl grid-cols-1 gap-6 md:grid-cols-2">
        {/* lista de exercícios */}
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
                      onChange={() => alternar(e)}
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

        {/* treino sendo editado */}
        <section className="flex h-[32rem] flex-col rounded-lg border border-black bg-white">
          <header className="flex items-center justify-between border-b border-black p-3">
            <select
              value={treinoAtual}
              onChange={(e) => {
                setTreinoAtual(e.target.value as Letra);
                setAberto(null);
                setErro("");
              }}
              className="border border-black px-2 py-1 font-bold"
            >
              {LETRAS.map((l) => (
                <option key={l} value={l}>
                  Treino {l}
                </option>
              ))}
            </select>

            <button
              type="button"
              onClick={salvar}
              disabled={salvando}
              className="rounded border border-black bg-blue-300 px-4 py-1 text-sm font-bold hover:bg-blue-400 disabled:opacity-50"
            >
              {salvando ? "Salvando..." : "Salvar"}
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
                const isOpen = aberto === item.chave;
                return (
                  <div key={item.chave} className="border border-black">
                    <button
                      type="button"
                      onClick={() => setAberto(isOpen ? null : item.chave)}
                      className="flex w-full items-center justify-between bg-blue-100 px-3 py-1 font-bold"
                    >
                      <span>{item.nome}</span>
                      <span className="text-xs">{isOpen ? "^" : "v"}</span>
                    </button>

                    {isOpen && (
                      <div className="space-y-2 bg-white p-3 text-sm">
                        <label className="flex items-center gap-2">
                          Séries:
                          <input
                            type="number"
                            min={1}
                            value={item.series}
                            onChange={(e) => editar(item.chave, "series", e.target.value)}
                            className="w-16 border-b border-black outline-none"
                          />
                        </label>
                        <label className="flex items-center gap-2">
                          Repetições:
                          <input
                            type="number"
                            min={1}
                            value={item.repeticoes}
                            onChange={(e) => editar(item.chave, "repeticoes", e.target.value)}
                            className="w-16 border-b border-black outline-none"
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

export default function EditarTreinoPage() {
  return (
    <Suspense fallback={<main className="mt-[10vh] text-center">Carregando...</main>}>
      <EditarTreino />
    </Suspense>
  );
}