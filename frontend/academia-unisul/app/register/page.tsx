"use client";

import { useState, type ChangeEvent, type SyntheticEvent } from "react";
import Link from "next/link";

const vazio = { nome: "", cpf: "", telefone: "", endereco: "" };

export default function RegisterPage() {
  const [form, setForm] = useState(vazio);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState("");
  const [sucesso, setSucesso] = useState(false);

  const atualizar =
    (campo: keyof typeof form) => (e: ChangeEvent<HTMLInputElement>) =>
      setForm({ ...form, [campo]: e.target.value });

  const campos: { campo: keyof typeof form; label: string }[] = [
    { campo: "nome", label: "Nome completo" },
    { campo: "cpf", label: "CPF" },
    { campo: "telefone", label: "Telefone" },
    { campo: "endereco", label: "Endereço" },
  ];

  async function cadastrar(e: SyntheticEvent<HTMLFormElement>) {
    e.preventDefault();
    setErro("");
    setSucesso(false);

    const cpf = form.cpf.replace(/\D/g, "");
    const telefone = form.telefone.replace(/\D/g, "");

    if (cpf.length !== 11) {
      setErro("O CPF deve ter 11 números");
      return;
    }

    setEnviando(true);
    try {
      const res = await fetch("/api/clientes", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: form.nome.trim(),
          cpf,
          telefone,
          address: form.endereco.trim(),
        }),
      });

      if (res.status === 409) {
        setErro("Já existe um aluno cadastrado com esse CPF");
        return;
      }
      if (!res.ok) {
        setErro("Não foi possível cadastrar. Verifique os dados e tente de novo");
        return;
      }

      setForm(vazio);
      setSucesso(true);
      setTimeout(() => setSucesso(false), 3000);
    } catch {
      setErro("Não foi possível conectar ao servidor");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <main style={{ margin: "10vh auto" }}>
      <header className="text-center">
        <Link href="/home" className="fixed top-4 left-4 z-50">Voltar</Link>
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
              required
              value={form[campo]}
              onChange={atualizar(campo)}
              className="mt-1 rounded border border-black px-2 py-1 font-normal"
            />
          </label>
        ))}

        <button
          type="submit"
          disabled={enviando}
          className="mx-auto mt-6 rounded border border-black bg-blue-300 px-8 py-1 font-bold hover:bg-blue-400 disabled:opacity-50"
        >
          {enviando ? "Cadastrando..." : "Cadastrar"}
        </button>

        {erro && <p className="text-center text-sm font-bold text-red-600">{erro}</p>}
        {sucesso && (
          <p className="text-center text-sm font-bold text-green-700">
            Aluno cadastrado com sucesso!
          </p>
        )}
      </form>
    </main>
  );
}