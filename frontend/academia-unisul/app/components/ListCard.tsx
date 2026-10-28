"use client";

import Link from "next/link";
import { useState } from "react";

export type ListItem = {
  id: number | string;
  titulo: string;
  detalhe?: string;
};

type ListCardProps = {
  title: string;
  items: ListItem[];
  searchPlaceholder?: string;
  actionLabel: string;
  actionHref: string;
  onSelect?: (item: ListItem) => void;
};

const normalize = (s: string) =>
  s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();

export default function ListCard({
  title,
  items,
  searchPlaceholder = "Pesquisar nome",
  actionLabel,
  actionHref,
  onSelect,
}: ListCardProps) {
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<ListItem["id"] | null>(null);

  const filtered = items.filter((i) =>
    normalize(i.titulo).includes(normalize(search))
  );

  return (
    <section className="flex h-[32rem] flex-col rounded-lg border border-black bg-white">
      <header className="flex items-center justify-between gap-4 p-3">
        <h2 className="text-lg font-bold">{title}</h2>
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={searchPlaceholder}
          className="w-40 border-b border-black text-sm outline-none"
        />
      </header>

      <ul className="flex-1 overflow-y-auto border-y border-black">
        {filtered.length === 0 && (
          <li className="p-3 text-sm text-gray-500">Nenhum resultado</li>
        )}
        {filtered.map((item) => (
          <li key={item.id}>
            <button
              type="button"
              onClick={() => {
                setSelectedId(item.id);
                onSelect?.(item);
              }}
              className={`flex h-10 w-full items-center justify-between border-b border-black px-3 text-left ${
                selectedId === item.id
                  ? "bg-blue-300"
                  : "odd:bg-white even:bg-gray-300 hover:bg-blue-100"
              }`}
            >
              <span>{item.titulo}</span>
              {item.detalhe && (
                <span className="text-sm text-gray-600">{item.detalhe}</span>
              )}
            </button>
          </li>
        ))}
      </ul>

      <Link href={actionHref} className="m-2 rounded border border-black p-2 font-bold">
        + {actionLabel}
      </Link>
    </section>
  );
}