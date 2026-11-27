"use client";

import { useState, type SyntheticEvent } from "react";
import type { Treino } from "../components/BoxTreino";

type TreinoApi = {
  exercicios: {
    id_treino: number;
    id_set: number;
    nome: string;
    sets: { sets: number; repeticoes: number };
  }[];
};
type HistoricoApi = { id: number; id_membro: number; id_treino: number; dia: string };
type Registro = { id: number; treino: string; data: string };
type AlunoView = {
  id: number;
  fichas: (number | null)[];
  nome: string;
  treinos: Treino[];
  historico: Registro[] | null;
};


const campo = (o: Record<string, unknown>, nome: string) =>
  o[Object.keys(o).find((k) => k.toLowerCase() === nome) ?? ""];


const formatarData = (dia: string) => {
  const [a, m, d] = dia.split("-");
  return `${d}/${m}/${a}`;
};

export default function AlunoPage() {
  const [entrada, setEntrada] = useState("");
  const [aluno, setAluno] = useState<AlunoView | null>(null);
  const [aberto, setAberto] = useState<string | null>(null);
  const [erro, setErro] = useState("");
  const [buscando, setBuscando] = useState(false);

  async function buscar(e: SyntheticEvent<HTMLFormElement>) {
    e.preventDefault();
    setErro("");
    setAluno(null);
    setAberto(null);

    const digitos = entrada.replace(/\D/g, "");
    if (!digitos) {
      setErro("Informe seu ID ou CPF");
      return;
    }

    setBuscando(true);
    try {
      let membro: Record<string, unknown> | null = null;

      if (digitos.length === 11) {
        const r = await fetch("/api/clientes");
        if (!r.ok) throw new Error();
        const lista: Record<string, unknown>[] = await r.json();
        membro =
          lista.find(
            (c) => String(campo(c, "cpf")).replace(/\D/g, "") === digitos
          ) ?? null;
      } else {
        const r = await fetch(`/api/clientes/${digitos}`);
        if (r.ok) membro = await r.json();
        else if (r.status !== 404) throw new Error();
      }

      if (!membro) {
        setErro("Aluno não encontrado");
        return;
      }

      const id = Number(campo(membro, "id"));
      const nome = String(campo(membro, "nome"));

      const fichas = ["id_treino_a", "id_treino_b", "id_treino_c"].map((k) => {
        const v = campo(membro, k);
        return v == null ? null : Number(v);
      });

      const [rt, rh] = await Promise.all([
        fetch(`/api/treinos/${id}`),
        fetch(`/api/historico-treinos?id_membro=${id}`),
      ]);
      if (!rt.ok) throw new Error();
      const { treinos }: { treinos: TreinoApi[] } = await rt.json();


      let historico: Registro[] | null = null;
      if (rh.ok) {
        const regs: HistoricoApi[] = await rh.json();
        historico = regs.slice(0, 10).map((h) => {
          const i = fichas.indexOf(h.id_treino);
          return {
            id: h.id,
            treino: i >= 0 ? `Treino ${"ABC"[i]}` : "Treino",
            data: formatarData(h.dia),
          };
        });
      }

      setAluno({
        id,
        fichas,
        nome,
        historico,
        treinos: treinos.map((t, i) => ({
          id: "ABC"[i],
          nome: `Treino ${"ABC"[i]}`,
          exercicios: t.exercicios.map((ex) => ({
            idSet: ex.id_set,
            idTreino: ex.id_treino,
            nome: ex.nome,
            series: ex.sets.sets,
            repeticoes: ex.sets.repeticoes,
          })),
        })),
      });
    } catch {
      setErro("Não foi possível buscar. Tente de novo");
    } finally {
      setBuscando(false);
    }
  }

  async function imprimir(t: Treino) {
    if (!aluno) return;

    const idTreino = aluno.fichas["ABC".indexOf(t.id)];
    if (idTreino === null || idTreino === undefined) {
      setErro("Este treino não está disponível para impressão");
      return;
    }

    setErro("");
    try {
      const r = await fetch("/api/historico-treinos", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ id_membro: aluno.id, id_treino: idTreino }),
      });
      if (!r.ok) throw new Error();
    } catch {
      setErro("Não foi possível registrar no histórico. O treino não foi impresso");
      return;
    }

    window.addEventListener("afterprint", () => window.location.reload(), { once: true });
    window.print();
  }

  const treinoAberto = aluno?.treinos.find((t) => t.id === aberto) ?? null;

  return (
    <main style={{ margin: "10vh auto" }}>

      <div className="print:hidden">
        <header className="main-header">
          <h1>ACADEMIA UNISUL</h1>
          <p>Imprimir treino</p>
        </header>

        <div className="mx-auto mt-10 w-full max-w-sm px-4">
          <form onSubmit={buscar} className="flex items-center gap-2">
            <input
              type="text"
              value={entrada}
              onChange={(e) => setEntrada(e.target.value)}
              placeholder="Informe seu ID ou CPF"
              className="w-full border border-black px-2 py-1"
            />
            <button
              type="submit"
              disabled={buscando}
              aria-label="Buscar"
              className="h-8 w-8 shrink-0 rounded-full border border-black bg-blue-300 font-bold hover:bg-blue-400 disabled:opacity-50"
            >
              {buscando ? "…" : "→"}
            </button>
          </form>

          {erro && (
            <p className="mt-3 text-center text-sm font-bold text-red-600">{erro}</p>
          )}

          {aluno && (
            <>
              <p className="mt-6 text-center font-bold">Olá, {aluno.nome}</p>

              <div className="mt-3 space-y-1">
                {aluno.treinos.map((t) => {
                  const isOpen = aberto === t.id;
                  return (
                    <div key={t.id} className="border border-black">
                      <button
                        type="button"
                        onClick={() => setAberto(isOpen ? null : t.id)}
                        className="flex w-full items-center justify-between bg-blue-100 px-3 py-1 text-sm font-bold"
                      >
                        <span>{t.nome}</span>
                        <span className="text-xs">{isOpen ? "^" : "v"}</span>
                      </button>

                      {isOpen && (
                        <div className="bg-white p-2">
                          {t.exercicios.length === 0 ? (
                            <p className="text-center text-sm text-gray-500">
                              Nenhum exercício neste treino ainda
                            </p>
                          ) : (
                            <ul className="pl-10">
                              {t.exercicios.map((ex) => (
                                <li key={ex.idSet}>
                                  {ex.nome}
                                  <span className="ml-2 text-xs text-gray-600">
                                    {ex.series}x {ex.repeticoes}
                                  </span>
                                </li>
                              ))}
                            </ul>
                          )}
                          <button
                            type="button"
                            onClick={() => imprimir(t)}
                            disabled={t.exercicios.length === 0}
                            className="mx-auto mt-2 block text-sm font-bold hover:underline disabled:opacity-40"
                          >
                            Imprimir Treino
                          </button>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              <div className="mt-8 border-t border-gray-400 pt-3">
                <h3 className="mb-1 text-sm font-bold">Histórico de atividades</h3>

                {aluno.historico === null && (
                  <p className="text-xs text-gray-500">Histórico indisponível no momento.</p>
                )}
                {aluno.historico?.length === 0 && (
                  <p className="text-xs text-gray-500">Nenhuma atividade registrada ainda.</p>
                )}
                {aluno.historico && aluno.historico.length > 0 && (
                  <ul className="space-y-1">
                    {aluno.historico.map((h) => (
                      <li
                        key={h.id}
                        className="flex items-center justify-between border border-black bg-blue-100 px-3 py-1 text-xs font-bold"
                      >
                        <span>{h.treino}</span>
                        <span>{h.data}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </>
          )}
        </div>
      </div>


      {aluno && treinoAberto && (
        <section className="hidden print:block">
          <h1 className="text-2xl font-bold">ACADEMIA UNISUL</h1>
          <p className="mt-2">Aluno: {aluno.nome}</p>
          <h2 className="mt-4 text-xl font-bold">{treinoAberto.nome}</h2>
          <ul className="mt-2 space-y-1">
            {treinoAberto.exercicios.map((ex) => (
              <li key={ex.idSet}>
                {ex.nome} — {ex.series} séries x {ex.repeticoes} repetições
              </li>
            ))}
          </ul>
        </section>
      )}
    </main>
  );
}