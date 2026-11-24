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

# ---------------------------------------------------------------------------
# Etapa 1: tabelas
# ---------------------------------------------------------------------------
if ! sqlite3 "$ARQUIVO_BANCO" <<'SQL'
.bail on
-- Garante a integridade das chaves estrangeiras
PRAGMA foreign_keys = ON;

-- Catalogo de exercicios
CREATE TABLE IF NOT EXISTS Exercicios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    descricao TEXT
);

-- Series de um exercicio (ex.: 3 sets x 12 repeticoes de supino)
CREATE TABLE IF NOT EXISTS Sets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    id_exercicio INTEGER NOT NULL,
    sets INTEGER NOT NULL,
    repeticoes INTEGER NOT NULL,

    FOREIGN KEY (id_exercicio)
        REFERENCES Exercicios(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CHECK (sets > 0),
    CHECK (repeticoes > 0)
);

-- Plano de treino (ficha). Os sets ficam na tabela de ligacao Treino_Sets.
CREATE TABLE IF NOT EXISTS Treino (
    id INTEGER PRIMARY KEY AUTOINCREMENT
);

-- Substitui a "lista_id_sets": uma linha por set dentro de um treino
CREATE TABLE IF NOT EXISTS Treino_Sets (
    id_treino INTEGER NOT NULL,
    id_set INTEGER NOT NULL,
    ordem INTEGER NOT NULL DEFAULT 0,

    PRIMARY KEY (id_treino, id_set),

    FOREIGN KEY (id_treino)
        REFERENCES Treino(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    FOREIGN KEY (id_set)
        REFERENCES Sets(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Membros (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    cpf TEXT NOT NULL UNIQUE,
    telefone TEXT,
    endereco TEXT,
    data_cadastro TEXT NOT NULL,
    id_treino_a INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL,
    id_treino_b INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL,
    id_treino_c INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Funcionarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    cargo TEXT NOT NULL,
    login TEXT NOT NULL UNIQUE,
    senha_hash TEXT NOT NULL,
    senha_salt TEXT NOT NULL
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

-- Validade da assinatura de cada membro (uma linha por membro)
CREATE TABLE IF NOT EXISTS Validade_Assinaturas (
    id_membro INTEGER PRIMARY KEY
        REFERENCES Membros(id) ON UPDATE CASCADE ON DELETE CASCADE,
    data_final TEXT NOT NULL
);

-- Dias em que um membro realizou uma ficha de treino (A, B ou C)
CREATE TABLE IF NOT EXISTS Historico_Treinos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    id_membro INTEGER NOT NULL,
    id_treino INTEGER NOT NULL,
    dia TEXT NOT NULL,

    FOREIGN KEY (id_membro)
        REFERENCES Membros(id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    FOREIGN KEY (id_treino)
        REFERENCES Treino(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Current_sessions (
    session_token TEXT NOT NULL PRIMARY KEY,
    last_used TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Token com exatamente 32 caracteres
    CHECK (length(session_token) = 32)
);
SQL
then
    echo "Erro ao criar as tabelas do banco de dados." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# Etapa 2: migracao para bancos que ja existiam sem as colunas de treino em Membros
# (CREATE TABLE IF NOT EXISTS nao altera tabelas existentes)
# ---------------------------------------------------------------------------
for coluna in id_treino_a id_treino_b id_treino_c; do
    existe=$(sqlite3 "$ARQUIVO_BANCO" \
        "SELECT COUNT(*) FROM pragma_table_info('Membros') WHERE name = '$coluna';") || {
        echo "Erro ao verificar a coluna $coluna." >&2
        exit 1
    }

    if [ "$existe" = "0" ]; then
        if ! sqlite3 "$ARQUIVO_BANCO" \
            "PRAGMA foreign_keys = ON;
             ALTER TABLE Membros ADD COLUMN $coluna INTEGER
                 REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL;"; then
            echo "Erro ao adicionar a coluna $coluna em Membros." >&2
            exit 1
        fi
        echo "Coluna $coluna adicionada em Membros."
    fi
done

# ---------------------------------------------------------------------------
# Etapa 3: indices
# ---------------------------------------------------------------------------
if sqlite3 "$ARQUIVO_BANCO" <<'SQL'
.bail on
PRAGMA foreign_keys = ON;

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

CREATE INDEX IF NOT EXISTS idx_historico_treinos_membro_dia
    ON Historico_Treinos(id_membro, dia);

CREATE INDEX IF NOT EXISTS idx_historico_treinos_treino
    ON Historico_Treinos(id_treino);

CREATE INDEX IF NOT EXISTS idx_current_sessions_last_used
    ON Current_sessions(last_used);

CREATE INDEX IF NOT EXISTS idx_sets_exercicio
    ON Sets(id_exercicio);

CREATE INDEX IF NOT EXISTS idx_treino_sets_set
    ON Treino_Sets(id_set);

CREATE INDEX IF NOT EXISTS idx_membros_treino_a
    ON Membros(id_treino_a);

CREATE INDEX IF NOT EXISTS idx_membros_treino_b
    ON Membros(id_treino_b);

CREATE INDEX IF NOT EXISTS idx_membros_treino_c
    ON Membros(id_treino_c);
SQL
then
    echo "Banco de dados criado/verificado com sucesso!"
    echo "Arquivo: $ARQUIVO_BANCO"
else
    echo "Erro ao criar os indices do banco de dados." >&2
    exit 1
fi
