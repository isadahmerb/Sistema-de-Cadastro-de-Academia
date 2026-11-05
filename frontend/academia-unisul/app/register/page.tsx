"use client";

import { useState } from "react";

export default function RegisterPage() {
    const [form, setForm] = useState({
        nome: "",
        cpf: "",
        telefone: "",
        endereco: "",
    });

    const atualizar =
        (campo: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
            setForm({ ...form, [campo]: e.target.value });

    const cadastrar = (e: React.FormEvent) => {
        e.preventDefault();
        // TODO: integrar com o backend 
        console.log("Cadastrar aluno:", form);
        setForm({ nome: "", cpf: "", telefone: "", endereco: "" });
    };

    const campos: { campo: keyof typeof form; label: string }[] = [
        { campo: "nome", label: "Nome completo" },
        { campo: "cpf", label: "CPF" },
        { campo: "telefone", label: "Telefone" },
        { campo: "endereco", label: "Endereço" },
    ];

    return (
        <main style={{ margin: "10vh auto" }}>
            <header className="text-center">
                <h1 className="text-2xl font-bold">ACADEMIA UNISUL</h1>
                <p className="font-bold">Cadastrar Aluno</p>
            </header>

            <form
                onSubmit={cadastrar}
                className="mx-auto mt-12 flex w-full max-w-xs flex-col gap-4"
            >
                {campos.map(({ campo, label }) => (
                    <label key={campo} className="flex flex-col font-bold">
                        {label}
                        <input
                            type="text"
                            value={form[campo]}
                            onChange={atualizar(campo)}
                            className="mt-1 rounded border border-black px-2 py-1 font-normal"
                        />
                    </label>
                ))}

                <button
                    type="submit"
                    className="mx-auto mt-6 rounded border border-black bg-blue-300 px-8 py-1 font-bold hover:bg-blue-400"
                >
                    Cadastrar
                </button>
            </form>
        </main>
    );
}