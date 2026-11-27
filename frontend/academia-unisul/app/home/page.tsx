"use client";

import { useEffect, useState } from "react";
import ListCard, { type ListItem } from "../components/ListCard";
import BoxTreino, { type Aluno, type Atividade, type Treino } from "../components/BoxTreino";

type Membro = { id: number; nome: string; fichas: (number | null)[] };
type HistoricoApi = { id: number; id_membro: number; id_treino: number; dia: string };
type Detalhe = { id: number; treinos: Treino[]; historico: Atividade[] };

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

const idOuNull = (v: unknown) => (v == null ? null : Number(v));


const formatarData = (dia: string) => {
    const [a, m, d] = dia.split("-");
    return `${d}/${m}/${a}`;
};

export default function HomePage() {
    const [membros, setMembros] = useState<Membro[]>([]);
    const [alunoId, setAlunoId] = useState<number | null>(null);
    const [detalhe, setDetalhe] = useState<Detalhe | null>(null);
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
                        fichas: ["id_treino_a", "id_treino_b", "id_treino_c"].map((k) =>
                            idOuNull(campo(m, k))
                        ),
                    }))
                )
            )
            .catch(() => setErro("Não foi possível carregar os alunos."))
            .finally(() => setCarregando(false));
    }, []);

    // treinos e histórico do aluno selecionado
    useEffect(() => {
        if (alunoId === null) return;
        const id = alunoId;
        let cancelado = false;
        const fichas = membros.find((m) => m.id === id)?.fichas ?? [];

        async function carregar() {
            const [rt, rh] = await Promise.all([
                fetch(`/api/treinos/${id}`),
                fetch(`/api/historico-treinos?id_membro=${id}`),
            ]);
            if (!rt.ok) throw new Error();
            const { treinos }: { treinos: TreinoApi[] } = await rt.json();

            
            let historico: Atividade[] = [];
            if (rh.ok) {
                const regs: HistoricoApi[] = await rh.json();
                historico = regs.slice(0, 20).map((h) => {
                    const i = fichas.indexOf(h.id_treino);
                    return {
                        id: h.id,
                        treino: i >= 0 ? `Treino ${"ABC"[i]}` : "Treino",
                        data: formatarData(h.dia),
                        hora: "", 
                    };
                });
            }

            if (cancelado) return;
            setDetalhe({
                id,
                historico,
                treinos: treinos.map((t, i) => ({
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
        }

        carregar().catch(() => !cancelado && setDetalhe({ id, treinos: [], historico: [] }));

        return () => {
            cancelado = true;
        };
    }, [alunoId, membros]);

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
            pagamentos: [], 
            historico: detalhe?.id === membro.id ? detalhe.historico : [],
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