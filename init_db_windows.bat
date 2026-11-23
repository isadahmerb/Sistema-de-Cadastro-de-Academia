@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Cria o banco SQLite e suas tabelas.
rem Requer o cliente de linha de comando "sqlite3" no PATH.
rem Este script fica na mesma pasta do create_database.py.

rem Caminhos do projeto
set "DIR_SCRIPT=%~dp0"
set "RAIZ_PROJETO=%DIR_SCRIPT:~0,-1%"
set "DIRETORIO_BANCO=%RAIZ_PROJETO%\backend"
set "ARQUIVO_BANCO=%DIRETORIO_BANCO%\db.db"
set "ARQUIVO_SQL=%TEMP%\init_db_%RANDOM%.sql"
set "ARQUIVO_IDX=%TEMP%\init_db_idx_%RANDOM%.sql"

where sqlite3 >nul 2>&1
if errorlevel 1 goto :sem_sqlite

if not exist "%DIRETORIO_BANCO%" mkdir "%DIRETORIO_BANCO%"
if errorlevel 1 goto :erro_dir

rem Extrai o SQL embutido no final deste arquivo
rem   linhas ::SQL^| = tabelas
rem   linhas ::IDX^| = indices
call :extrair SQL "%ARQUIVO_SQL%"
call :extrair IDX "%ARQUIVO_IDX%"

rem Etapa 1: tabelas
sqlite3 "%ARQUIVO_BANCO%" < "%ARQUIVO_SQL%"
if errorlevel 1 goto :erro_sql

rem Etapa 2: migracao para bancos que ja existiam sem as colunas de treino em Membros
rem (CREATE TABLE IF NOT EXISTS nao altera tabelas existentes)
for %%C in (id_treino_a id_treino_b id_treino_c) do (
    call :migrar_coluna %%C
    if errorlevel 1 goto :erro_migracao
)

rem Etapa 3: indices
sqlite3 "%ARQUIVO_BANCO%" < "%ARQUIVO_IDX%"
if errorlevel 1 goto :erro_sql

del "%ARQUIVO_SQL%" >nul 2>&1
del "%ARQUIVO_IDX%" >nul 2>&1
echo Banco de dados criado/verificado com sucesso!
echo Arquivo: %ARQUIVO_BANCO%
exit /b 0

:sem_sqlite
echo Erro: sqlite3 nao encontrado no PATH.
echo Instale com: winget install SQLite.SQLite
exit /b 1

:erro_dir
echo Erro ao criar o diretorio do banco: %DIRETORIO_BANCO%
exit /b 1

:erro_sql
del "%ARQUIVO_SQL%" >nul 2>&1
del "%ARQUIVO_IDX%" >nul 2>&1
echo Erro ao criar o banco de dados.
exit /b 1

:erro_migracao
del "%ARQUIVO_SQL%" >nul 2>&1
del "%ARQUIVO_IDX%" >nul 2>&1
echo Erro ao migrar a tabela Membros.
exit /b 1

rem ---------------------------------------------------------------
rem Sub-rotina: extrai as linhas com o prefixo ::TAG^| para um arquivo
rem   %1 = TAG (SQL ou IDX)   %2 = arquivo de saida
rem ---------------------------------------------------------------
:extrair
if exist "%~2" del "%~2"
for /f "usebackq tokens=* delims=" %%A in (`findstr /b /c:"::%~1|" "%~f0"`) do (
    set "LINHA=%%A"
    set "LINHA=!LINHA:~6!"
    >>"%~2" echo(!LINHA!
)
exit /b 0

rem ---------------------------------------------------------------
rem Sub-rotina: adiciona a coluna %1 em Membros se ela ainda nao existir
rem ---------------------------------------------------------------
:migrar_coluna
set "EXISTE="
for /f "usebackq delims=" %%N in (`sqlite3 "%ARQUIVO_BANCO%" "SELECT COUNT(*) FROM pragma_table_info('Membros') WHERE name = '%~1';"`) do set "EXISTE=%%N"
if not defined EXISTE exit /b 1
if "!EXISTE!"=="0" (
    sqlite3 "%ARQUIVO_BANCO%" "PRAGMA foreign_keys = ON; ALTER TABLE Membros ADD COLUMN %~1 INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL;"
    if errorlevel 1 exit /b 1
    echo Coluna %~1 adicionada em Membros.
)
exit /b 0

rem ---------------------------------------------------------------
rem SQL embutido (nao remova os prefixos ::SQL^| e ::IDX^|)
rem ---------------------------------------------------------------
::SQL|.bail on
::SQL|-- Garante a integridade das chaves estrangeiras
::SQL|PRAGMA foreign_keys = ON;
::SQL|
::SQL|-- Catalogo de exercicios
::SQL|CREATE TABLE IF NOT EXISTS Exercicios (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    nome TEXT NOT NULL,
::SQL|    descricao TEXT
::SQL|);
::SQL|
::SQL|-- Series de um exercicio (ex.: 3 sets x 12 repeticoes de supino)
::SQL|CREATE TABLE IF NOT EXISTS Sets (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    id_exercicio INTEGER NOT NULL,
::SQL|    sets INTEGER NOT NULL,
::SQL|    repeticoes INTEGER NOT NULL,
::SQL|
::SQL|    FOREIGN KEY (id_exercicio)
::SQL|        REFERENCES Exercicios(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE RESTRICT,
::SQL|
::SQL|    CHECK (sets > 0),
::SQL|    CHECK (repeticoes > 0)
::SQL|);
::SQL|
::SQL|-- Plano de treino (ficha). Os sets ficam na tabela de ligacao Treino_Sets.
::SQL|CREATE TABLE IF NOT EXISTS Treino (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT
::SQL|);
::SQL|
::SQL|-- Substitui a lista_id_sets: uma linha por set dentro de um treino
::SQL|CREATE TABLE IF NOT EXISTS Treino_Sets (
::SQL|    id_treino INTEGER NOT NULL,
::SQL|    id_set INTEGER NOT NULL,
::SQL|    ordem INTEGER NOT NULL DEFAULT 0,
::SQL|
::SQL|    PRIMARY KEY (id_treino, id_set),
::SQL|
::SQL|    FOREIGN KEY (id_treino)
::SQL|        REFERENCES Treino(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE CASCADE,
::SQL|
::SQL|    FOREIGN KEY (id_set)
::SQL|        REFERENCES Sets(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE RESTRICT
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Membros (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    nome TEXT NOT NULL,
::SQL|    cpf TEXT NOT NULL UNIQUE,
::SQL|    telefone TEXT,
::SQL|    endereco TEXT,
::SQL|    data_cadastro TEXT NOT NULL,
::SQL|    id_treino_a INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL,
::SQL|    id_treino_b INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL,
::SQL|    id_treino_c INTEGER REFERENCES Treino(id) ON UPDATE CASCADE ON DELETE SET NULL
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Funcionarios (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    nome TEXT NOT NULL,
::SQL|    cargo TEXT NOT NULL,
::SQL|    login TEXT NOT NULL UNIQUE,
::SQL|    senha_hash TEXT NOT NULL,
::SQL|    senha_salt TEXT NOT NULL
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Treinos (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    id_membro INTEGER NOT NULL,
::SQL|    tipo TEXT NOT NULL,
::SQL|    descricao TEXT,
::SQL|    duracao_min INTEGER NOT NULL,
::SQL|    data_inicio TEXT NOT NULL,
::SQL|    data_fim TEXT,
::SQL|
::SQL|    FOREIGN KEY (id_membro)
::SQL|        REFERENCES Membros(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE CASCADE,
::SQL|
::SQL|    CHECK (duracao_min > 0),
::SQL|    CHECK (
::SQL|        data_fim IS NULL
::SQL|        OR data_fim >= data_inicio
::SQL|    )
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Pagamentos (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    id_membro INTEGER NOT NULL,
::SQL|    id_funcionario INTEGER NOT NULL,
::SQL|    valor NUMERIC NOT NULL,
::SQL|    data_pagamento TEXT NOT NULL,
::SQL|    status TEXT NOT NULL,
::SQL|
::SQL|    FOREIGN KEY (id_membro)
::SQL|        REFERENCES Membros(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE RESTRICT,
::SQL|
::SQL|    FOREIGN KEY (id_funcionario)
::SQL|        REFERENCES Funcionarios(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE RESTRICT,
::SQL|
::SQL|    CHECK (valor >= 0)
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Historico_Atividades (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    id_membro INTEGER NOT NULL,
::SQL|    id_funcionario INTEGER NOT NULL,
::SQL|    id_treino INTEGER,
::SQL|    atividade TEXT NOT NULL,
::SQL|    data TEXT NOT NULL,
::SQL|    tempo_execucao_min INTEGER,
::SQL|
::SQL|    FOREIGN KEY (id_membro)
::SQL|        REFERENCES Membros(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE CASCADE,
::SQL|
::SQL|    FOREIGN KEY (id_funcionario)
::SQL|        REFERENCES Funcionarios(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE RESTRICT,
::SQL|
::SQL|    FOREIGN KEY (id_treino)
::SQL|        REFERENCES Treinos(id)
::SQL|        ON UPDATE CASCADE
::SQL|        ON DELETE SET NULL,
::SQL|
::SQL|    CHECK (
::SQL|        tempo_execucao_min IS NULL
::SQL|        OR tempo_execucao_min >= 0
::SQL|    )
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Current_sessions (
::SQL|    session_token TEXT NOT NULL PRIMARY KEY,
::SQL|    last_used TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
::SQL|
::SQL|    -- Token com exatamente 32 caracteres
::SQL|    CHECK (length(session_token) = 32)
::SQL|);
::IDX|.bail on
::IDX|PRAGMA foreign_keys = ON;
::IDX|
::IDX|-- Indices para consultas pelas chaves estrangeiras
::IDX|CREATE INDEX IF NOT EXISTS idx_treinos_membro
::IDX|    ON Treinos(id_membro);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_pagamentos_membro
::IDX|    ON Pagamentos(id_membro);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_pagamentos_funcionario
::IDX|    ON Pagamentos(id_funcionario);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_historico_membro
::IDX|    ON Historico_Atividades(id_membro);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_historico_funcionario
::IDX|    ON Historico_Atividades(id_funcionario);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_historico_treino
::IDX|    ON Historico_Atividades(id_treino);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_current_sessions_last_used
::IDX|    ON Current_sessions(last_used);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_sets_exercicio
::IDX|    ON Sets(id_exercicio);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_treino_sets_set
::IDX|    ON Treino_Sets(id_set);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_membros_treino_a
::IDX|    ON Membros(id_treino_a);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_membros_treino_b
::IDX|    ON Membros(id_treino_b);
::IDX|
::IDX|CREATE INDEX IF NOT EXISTS idx_membros_treino_c
::IDX|    ON Membros(id_treino_c);
