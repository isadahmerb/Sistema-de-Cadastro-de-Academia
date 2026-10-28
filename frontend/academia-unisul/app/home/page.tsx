import ListCard, { type ListItem } from "../components/ListCard";

const alunos: ListItem[] = [
    { id: 1, titulo: "Maria Silva", detalhe: "1203" },
    { id: 2, titulo: "João Souza", detalhe: "1204" },
    { id: 3, titulo: "Joãoewqr ", detalhe: "1204" },
    { id: 4, titulo: "Joãorew Souza", detalhe: "1204" },
    { id: 5, titulo: "Joãot34 ", detalhe: "1204" },
    { id: 6, titulo: "João Souza", detalhe: "1204" },
    { id: 7, titulo: "Joãghgf ", detalhe: "1204" },
    { id: 8, titulo: "João768 Souza", detalhe: "1204" },
    { id: 9, titulo: "João5432 ", detalhe: "1204" },
    { id: 10, titulo: "Jobnvbão Souza", detalhe: "1204" },
    { id: 11, titulo: "João7869 ", detalhe: "1204" },
    { id: 12, titulo: "João123 Souza", detalhe: "1204" },
    { id: 13, titulo: "João mcbn", detalhe: "1204" },
    { id: 14, titulo: "Joãofdgh ", detalhe: "1204" },
    { id: 15, titulo: "Joãoabc  ", detalhe: "1204" },
];

export default function HomePage() {
    return (
        <main style={{ margin: "10vh auto" }}>
            <header className="main-header">
                <h1>ACADEMIA UNISUL</h1>
                <p>Perfil ADM</p>
            </header>
            <div className="body">
                <div className="content">
                    <ListCard
                        title="Lista de alunos"
                        items={alunos}
                        actionLabel="Cadastrar aluno"
                        actionHref="/home/alunos/novo"
                    />
                    <ListCard
                        title="Lista de alunos"
                        items={alunos}
                        actionLabel="Cadastrar aluno"
                        actionHref="/home/alunos/novo"
                    />
                </div>
            </div>

        </main>
    );
}