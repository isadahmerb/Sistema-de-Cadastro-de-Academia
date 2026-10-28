"use client";

import { useState } from "react";

export type Treino = { id: string; nome: string; exercicios: string[] };
export type Pagamento = {
  id: number;
  referencia: string;
  vencimento: string;
  valor: number;
  status: "Aberto" | "Pago";
};
export type Atividade = { id: number; treino: string; data: string; hora: string };
export type Aluno = {
  id: number;
  nome: string;
  matricula: string;
  treinos: Treino[];
  pagamentos: Pagamento[];
  historico: Atividade[];
};

const brl = (v: number) =>
  v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

const parseData = (s: string) => {
  const [d, m, a] = s.split("/").map(Number);
  return new Date(a, m - 1, d);
};

/* depois qnd tiver o back tem q mudar pra algo assim:
const parseData = (s: string) => {
  const [a, m, d] = s.split("-").map(Number); // formato ISO
  return new Date(a, m - 1, d);
}; 
*/

const situacao = (p: Pagamento): "pago" | "aberto" | "vencido" => {
  if (p.status === "Pago") return "pago";
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  return parseData(p.vencimento) < hoje ? "vencido" : "aberto";
};

const corPagamento = {
  pago: "bg-green-100",
  aberto: "bg-yellow-100",
  vencido: "bg-red-200",
};

export default function BoxTreino({ aluno }: { aluno: Aluno | null }) {
  const [aberto, setAberto] = useState<string | null>(null);
  const [filtro, setFiltro] = useState<"Aberto" | "Pago" | "Todos">("Aberto");

  if (!aluno) {
    return (
      <section className="flex h-[32rem] items-center justify-center rounded-lg border border-black bg-white text-gray-500">
        Selecione um aluno
      </section>
    );
  }

  const pagamentos = aluno.pagamentos.filter(
    (p) => filtro === "Todos" || p.status === filtro
  );

  return (
    <section className="flex h-[32rem] flex-col rounded-lg border border-black bg-white">
      <h2 className="border-b border-black p-3 text-lg font-bold">{aluno.nome}</h2>

      <div className="flex-1 space-y-3 overflow-y-auto p-3">
        {/* treino */}
        <div>
          <div className="mb-1 flex items-center justify-between">
            <h3 className="font-bold">Treino</h3>
            <button className="border border-black px-3 text-xs font-bold">Editar</button>
          </div>

          <div className="space-y-1">
            {aluno.treinos.map((t) => {
              const isOpen = aberto === t.id;
              return (
                <div key={t.id} className="border border-black">
                  <button
                    type="button"
                    onClick={() => setAberto(isOpen ? null : t.id)}
                    className="flex w-full items-center justify-between bg-blue-100 px-3 py-1 font-bold"
                  >
                    <span>{t.nome}</span>
                    <span className="text-xs">{isOpen ? "^" : "v"}</span>
                  </button>

                  {isOpen && (
                    <div className="bg-white p-2">
                      <ul className="pl-10">
                        {t.exercicios.map((ex) => (
                          <li key={ex}>{ex}</li>
                        ))}
                      </ul>
                      <button
                        type="button"
                        onClick={() => window.print()}
                        className="mx-auto mt-2 block text-sm font-bold hover:underline"
                      >
                        Imprimir Treino
                      </button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* pagamentos */}
        <div className="border-t border-black pt-2">
          <div className="mb-1 flex items-center justify-between">
            <h3 className="font-bold">Pagamentos</h3>
            <select
              value={filtro}
              onChange={(e) => setFiltro(e.target.value as typeof filtro)}
              className="border border-black px-2 text-sm"
            >
              <option>Aberto</option>
              <option>Pago</option>
              <option>Todos</option>
            </select>
          </div>

          <ul className="space-y-1">
            {pagamentos.map((p) => (
              <li
                key={p.id}
                className={`flex items-center justify-between border border-black px-3 py-1 text-xs font-bold ${corPagamento[situacao(p)]}`}
              >
                <span>{p.referencia}</span>
                <span>VENCIMENTO {p.vencimento}</span>
                <span>{brl(p.valor)}</span>
              </li>
            ))}
          </ul>
          <p className="mt-2 border-b border-black pb-2 text-center text-xs">
            Não há mais registros
          </p>
        </div>

        {/* historico de atv */}
        <div>
          <h3 className="mb-1 font-bold">Histórico de atividades</h3>
          <ul className="space-y-1">
            {aluno.historico.map((h) => (
              <li
                key={h.id}
                className="flex items-center justify-between border border-black bg-blue-100 px-3 py-1 text-xs font-bold"
              >
                <span>{h.treino}</span>
                <span>
                  {h.data} <span className="text-[10px] font-normal">{h.hora}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}