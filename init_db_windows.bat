@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Cria o banco SQLite e suas tabelas.
rem Requer o cliente de linha de comando "sqlite3" no PATH.
rem Este script fica na mesma pasta do create_database.py.

rem Caminhos do projeto
set "DIR_SCRIPT=%~dp0"
for %%I in ("%DIR_SCRIPT%..") do set "RAIZ_PROJETO=%%~fI"
set "DIRETORIO_BANCO=%RAIZ_PROJETO%\Sistema-de-Cdastro-de-Academia\backend"
set "ARQUIVO_BANCO=%DIRETORIO_BANCO%\db.db"
set "ARQUIVO_SQL=%TEMP%\init_db_%RANDOM%.sql"

where sqlite3 >nul 2>&1
if errorlevel 1 goto :sem_sqlite

if not exist "%DIRETORIO_BANCO%" mkdir "%DIRETORIO_BANCO%"
if errorlevel 1 goto :erro_dir

rem Extrai o SQL embutido no final deste arquivo (linhas iniciadas por ::SQL^|)
if exist "%ARQUIVO_SQL%" del "%ARQUIVO_SQL%"
for /f "usebackq tokens=* delims=" %%A in (`findstr /b /c:"::SQL|" "%~f0"`) do (
    set "LINHA=%%A"
    set "LINHA=!LINHA:~6!"
    >>"%ARQUIVO_SQL%" echo(!LINHA!
)

sqlite3 "%ARQUIVO_BANCO%" < "%ARQUIVO_SQL%"
if errorlevel 1 goto :erro_sql

del "%ARQUIVO_SQL%" >nul 2>&1
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
echo Erro ao criar o banco de dados.
exit /b 1

rem ---------------------------------------------------------------
rem SQL embutido (nao remova o prefixo ::SQL^|)
rem ---------------------------------------------------------------
::SQL|.bail on
::SQL|-- Garante a integridade das chaves estrangeiras
::SQL|PRAGMA foreign_keys = ON;
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Membros (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    nome TEXT NOT NULL,
::SQL|    cpf TEXT NOT NULL UNIQUE,
::SQL|    telefone TEXT,
::SQL|    endereco TEXT,
::SQL|    data_cadastro TEXT NOT NULL
::SQL|);
::SQL|
::SQL|CREATE TABLE IF NOT EXISTS Funcionarios (
::SQL|    id INTEGER PRIMARY KEY AUTOINCREMENT,
::SQL|    nome TEXT NOT NULL,
::SQL|    cargo TEXT NOT NULL,
::SQL|    login TEXT NOT NULL UNIQUE,
::SQL|    senha_hash TEXT NOT NULL
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
::SQL|-- Indices para consultas pelas chaves estrangeiras
::SQL|CREATE INDEX IF NOT EXISTS idx_treinos_membro
::SQL|    ON Treinos(id_membro);
::SQL|
::SQL|CREATE INDEX IF NOT EXISTS idx_pagamentos_membro
::SQL|    ON Pagamentos(id_membro);
::SQL|
::SQL|CREATE INDEX IF NOT EXISTS idx_pagamentos_funcionario
::SQL|    ON Pagamentos(id_funcionario);
::SQL|
::SQL|CREATE INDEX IF NOT EXISTS idx_historico_membro
::SQL|    ON Historico_Atividades(id_membro);
::SQL|
::SQL|CREATE INDEX IF NOT EXISTS idx_historico_funcionario
::SQL|    ON Historico_Atividades(id_funcionario);
::SQL|
::SQL|CREATE INDEX IF NOT EXISTS idx_historico_treino
::SQL|    ON Historico_Atividades(id_treino);