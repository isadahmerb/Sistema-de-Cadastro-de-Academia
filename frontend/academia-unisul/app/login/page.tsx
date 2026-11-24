"use client";

import { useRouter } from "next/navigation";
import { useState, type SyntheticEvent } from "react";

export default function LoginPage() {
  const router = useRouter();
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function handleSubmit(e: SyntheticEvent<HTMLFormElement>) {
    e.preventDefault();
    setErro("");
    setEnviando(true);

    const dados = new FormData(e.currentTarget);

    try {
      const res = await fetch("/api/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          login: dados.get("codigo"),
          password: dados.get("password"),
        }),
      });

      if (res.status === 401) {
        setErro("Login ou senha inválidos");
        return;
      }
      if (!res.ok) {
        setErro("Não foi possível entrar. Tente de novo");
        return;
      }

      const { token } = await res.json();
      localStorage.setItem("token", token);
      router.push("/home");
    } catch {
      setErro("Não foi possível conectar ao servidor");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <main style={{ maxWidth: 360, margin: "10vh auto" }}>
      <header className="main-header">
        <h1>ACADEMIA UNISUL</h1>
        <p>Perfil ADM</p>
      </header>
      <form className="login-form" onSubmit={handleSubmit}>
        <p>Login de Admin</p>
        <input name="codigo" type="text" placeholder="abc123" required />
        <p>Senha de Admin</p>
        <input name="password" type="password" placeholder="******" required />
        {erro && <p className="text-sm font-bold text-red-600">{erro}</p>}
        <button type="submit" disabled={enviando}>
          {enviando ? "Entrando..." : "Entrar"}
        </button>
      </form>
    </main>
  );
}