#!/usr/bin/env bash
# Cria o banco SQLite e suas tabelas.
# Requer o cliente de linha de comando "sqlite3".

set -uo pipefail

# Caminhos do projeto (este script fica na raiz do projeto, ao lado da pasta backend)
DIR_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RAIZ_PROJETO="$DIR_SCRIPT"
DIRETORIO_BANCO="$RAIZ_PROJETO/backend"
ARQUIVO_BANCO="$DIRETORIO_BANCO/db.db"

if ! command -v sqlite3 >/dev/null 2>&1; then
    echo "Erro: sqlite3 nao encontrado. Instale com: sudo apt install sqlite3" >&2
    exit 1
fi

if ! mkdir -p "$DIRETORIO_BANCO"; then
    echo "Erro ao criar o diretorio do banco: $DIRETORIO_BANCO" >&2
    exit 1
fi

if sqlite3 "$ARQUIVO_BANCO" <<'SQL'
.bail on
-- Garante a integridade das chaves estrangeiras
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Membros (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    cpf TEXT NOT NULL UNIQUE,
    telefone TEXT,
    endereco TEXT,
    data_cadastro TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Funcionarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    cargo TEXT NOT NULL,
    login TEXT NOT NULL UNIQUE,
    senha_hash TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Treinos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    id_membro INTEGER NOT NULL,
    tipo TEXT NOT NULL,
    descricao TEXT,
    duracao_min INTEGER NOT NULL,
    data_inicio TEXT NOT NULL,
    data_fim TEXT,

    FOREIGN KEY (id_membro)
        REFERENCES Membros(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CHECK (duracao_min > 0),
    CHECK (
        data_fim IS NULL
        OR data_fim >= data_inicio
    )
);

CREATE TABLE IF NOT EXISTS Pagamentos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    id_membro INTEGER NOT NULL,
    id_funcionario INTEGER NOT NULL,
    valor NUMERIC NOT NULL,
    data_pagamento TEXT NOT NULL,
    status TEXT NOT NULL,

    FOREIGN KEY (id_membro)
        REFERENCES Membros(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    FOREIGN KEY (id_funcionario)
        REFERENCES Funcionarios(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CHECK (valor >= 0)
);

CREATE TABLE IF NOT EXISTS Historico_Atividades (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    id_membro INTEGER NOT NULL,
    id_funcionario INTEGER NOT NULL,
    id_treino INTEGER,
    atividade TEXT NOT NULL,
    data TEXT NOT NULL,
    tempo_execucao_min INTEGER,

    FOREIGN KEY (id_membro)
        REFERENCES Membros(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    FOREIGN KEY (id_funcionario)
        REFERENCES Funcionarios(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    FOREIGN KEY (id_treino)
        REFERENCES Treinos(id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CHECK (
        tempo_execucao_min IS NULL
        OR tempo_execucao_min >= 0
    )
);

-- Indices para consultas pelas chaves estrangeiras
CREATE INDEX IF NOT EXISTS idx_treinos_membro
    ON Treinos(id_membro);

CREATE INDEX IF NOT EXISTS idx_pagamentos_membro
    ON Pagamentos(id_membro);

CREATE INDEX IF NOT EXISTS idx_pagamentos_funcionario
    ON Pagamentos(id_funcionario);

CREATE INDEX IF NOT EXISTS idx_historico_membro
    ON Historico_Atividades(id_membro);

CREATE INDEX IF NOT EXISTS idx_historico_funcionario
    ON Historico_Atividades(id_funcionario);

CREATE INDEX IF NOT EXISTS idx_historico_treino
    ON Historico_Atividades(id_treino);
SQL
then
    echo "Banco de dados criado/verificado com sucesso!"
    echo "Arquivo: $ARQUIVO_BANCO"
else
    echo "Erro ao criar o banco de dados." >&2
    exit 1
fi
