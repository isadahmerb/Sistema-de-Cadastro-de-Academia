"use client";

import { useRouter } from "next/navigation";
import type { SyntheticEvent } from "react";



export default function LoginPage() {
  const router = useRouter();

  // TODO: tem q colocar verificacao no backend

  function handleSubmit(e: SyntheticEvent<HTMLFormElement>) {
    e.preventDefault();
    console.log("clicou");
    router.push("/home");
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
        <button type="submit">Entrar</button>
      </form>
    </main>
  );
}