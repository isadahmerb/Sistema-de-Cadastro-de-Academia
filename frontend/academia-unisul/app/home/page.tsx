"use client";

import { useState } from "react";
import ListCard, { type ListItem } from "../components/ListCard";
import BoxTreino, { type Aluno } from "../components/BoxTreino";

const alunos: Aluno[] = [
    {
        id: 1,
        nome: "Maria Silva",
        matricula: "1203",
        treinos: [
            { id: "A", nome: "Treino A", exercicios: ["Supino reto", "Crucifixo", "Tríceps testa", "Tríceps corda"] },
            { id: "B", nome: "Treino B", exercicios: ["Puxada frontal", "Remada baixa", "Rosca direta", "Rosca martelo"] },
            { id: "C", nome: "Treino C", exercicios: ["Agachamento", "Leg press", "Cadeira extensora", "Panturrilha"] },
        ],
        pagamentos: [
            { id: 1, referencia: "Outubro 2026", vencimento: "15/10/2026", valor: 100, status: "Aberto" },
            { id: 2, referencia: "Setembro 2026", vencimento: "15/09/2026", valor: 100, status: "Pago" },
        ],
        historico: [
            { id: 1, treino: "Treino A", data: "08/10/2026", hora: "14:30h" },
            { id: 2, treino: "Treino C", data: "06/10/2026", hora: "14:30h" },
            { id: 3, treino: "Treino B", data: "05/10/2026", hora: "14:30h" },
        ],
    },
    {
        id: 2,
        nome: "João Souza",
        matricula: "1204",
        treinos: [
            { id: "A", nome: "Treino A", exercicios: ["Desenvolvimento", "Elevação lateral", "Remada alta"] },
            { id: "B", nome: "Treino B", exercicios: ["Barra fixa", "Remada curvada", "Rosca alternada"] },
        ],
        pagamentos: [
            { id: 1, referencia: "Outubro 2026", vencimento: "10/10/2026", valor: 120, status: "Pago" },
            { id: 2, referencia: "Novembro 2026", vencimento: "10/11/2026", valor: 120, status: "Pago" },
        ],
        historico: [{ id: 1, treino: "Treino A", data: "07/10/2026", hora: "18:00h" }],
    },
    {
        id: 3,
        nome: "Leonardo",
        matricula: "1205",
        treinos: [
            { id: "A", nome: "Treino A", exercicios: ["Desenvolvimento", "Elevação lateral", "Remada alta"] },
            { id: "B", nome: "Treino B", exercicios: ["Barra fixa", "Remada curvada", "Rosca alternada"] },
            { id: "C", nome: "Treino C", exercicios: ["Agachamento", "Leg press", "Cadeira extensora", "Panturrilha"] },
        ],
        pagamentos: [
            { id: 1, referencia: "Outubro 2026", vencimento: "10/10/2026", valor: 100, status: "Pago" },
            { id: 2, referencia: "Novembro 2026", vencimento: "10/11/2026", valor: 100, status: "Aberto" },
        ],
        historico: [
            { id: 1, treino: "Treino C", data: "08/10/2026", hora: "18:00h" },
            { id: 2, treino: "Treino B", data: "06/10/2026", hora: "16:30h" },
            { id: 3, treino: "Treino A", data: "05/10/2026", hora: "11:30h" },
        ],
    },
];

const itens: ListItem[] = alunos.map((a) => ({
    id: a.id,
    titulo: a.nome,
    detalhe: a.matricula,
}));

export default function HomePage() {
    const [alunoId, setAlunoId] = useState<number | null>(null);
    const aluno = alunos.find((a) => a.id === alunoId) ?? null;
    return (
        <main style={{ margin: "10vh auto" }}>
            <header className="main-header">
                <h1>ACADEMIA UNISUL</h1>
                <p>Perfil ADM</p>
            </header>

            <div className="mx-auto mt-10 grid max-w-5xl grid-cols-1 gap-6 md:grid-cols-2">
                <ListCard
                    title="Lista de membros"
                    items={itens}
                    actionLabel="Cadastrar membro"
                    actionHref="/addmembros"
                    onSelect={(item) => setAlunoId(Number(item.id))}
                />
                <BoxTreino aluno={aluno} />
            </div>


        </main>
    );
}