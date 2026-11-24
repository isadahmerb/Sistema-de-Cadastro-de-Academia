"use client";

import { useEffect, useState } from "react";
import ListCard, { type ListItem } from "../components/ListCard";
import BoxTreino, { type Aluno, type Treino } from "../components/BoxTreino";

type Membro = { id: number; nome: string };
type TreinoApi = {
    exercicios: {
        id_treino: number;
        id_set: number;
        nome: string;
        sets: { sets: number; repeticoes: number };
    }[];
};


const campo = (o: Record<string, unknown>, nome: string) =>
    o[Object.keys(o).find((k) => k.toLowerCase() === nome) ?? ""];

export default function HomePage() {
    const [membros, setMembros] = useState<Membro[]>([]);
    const [alunoId, setAlunoId] = useState<number | null>(null);
    const [detalhe, setDetalhe] = useState<{ id: number; treinos: Treino[] } | null>(null);
    const [carregando, setCarregando] = useState(true);
    const [erro, setErro] = useState("");

    // lista de alunos
    useEffect(() => {
        fetch("/api/clientes")
            .then((r) => {
                if (!r.ok) throw new Error();
                return r.json();
            })
            .then((lista: Record<string, unknown>[]) =>
                setMembros(
                    lista.map((m) => ({
                        id: Number(campo(m, "id")),
                        nome: String(campo(m, "nome")),
                    }))
                )
            )
            .catch(() => setErro("Não foi possível carregar os alunos."))
            .finally(() => setCarregando(false));
    }, []);

    // treinos do aluno selecionado
    useEffect(() => {
        if (alunoId === null) return;
        let cancelado = false;

        fetch(`/api/treinos/${alunoId}`)
            .then((r) => {
                if (!r.ok) throw new Error();
                return r.json();
            })
            .then((d: { treinos: TreinoApi[] }) => {
                if (cancelado) return;
                setDetalhe({
                    id: alunoId,
                    treinos: d.treinos.map((t, i) => ({
                        id: "ABC"[i],
                        nome: `Treino ${"ABC"[i]}`,
                        exercicios: t.exercicios.map((e) => ({
                            idSet: e.id_set,
                            idTreino: e.id_treino,
                            nome: e.nome,
                            series: e.sets.sets,
                            repeticoes: e.sets.repeticoes,
                        })),
                    })),
                });
            })
            .catch(() => !cancelado && setDetalhe({ id: alunoId, treinos: [] }));

        return () => {
            cancelado = true;
        };
    }, [alunoId]);

    const itens: ListItem[] = membros.map((m) => ({
        id: m.id,
        titulo: m.nome,
        detalhe: String(m.id),
    }));

    const membro = membros.find((m) => m.id === alunoId);
    const aluno: Aluno | null = membro
        ? {
              id: membro.id,
              nome: membro.nome,
              matricula: String(membro.id),
              treinos: detalhe?.id === membro.id ? detalhe.treinos : [],
              pagamentos: [], // backend  não tem
              historico: [], // backend não tem
          }
        : null;

    return (
        <main style={{ margin: "10vh auto" }}>
            <header className="main-header">
                <h1>ACADEMIA UNISUL</h1>
                <p>Perfil ADM</p>
            </header>

            {erro && <p className="mt-6 text-center font-bold text-red-600">{erro}</p>}
            {carregando && <p className="mt-6 text-center">Carregando...</p>}

            {!carregando && !erro && (
                <div className="mx-auto mt-10 grid max-w-5xl grid-cols-1 gap-6 md:grid-cols-2">
                    <ListCard
                        title="Lista de alunos"
                        items={itens}
                        actionLabel="Cadastrar aluno"
                        actionHref="/register"
                        onSelect={(item) => setAlunoId(Number(item.id))}
                    />
                    <BoxTreino aluno={aluno} />
                </div>
            )}
        </main>
    );
}