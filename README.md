# A3 - Gestão e Qualidade de Software - Sistema-de-Cadastro-de-Academia

## Inicialização do banco de dados

Antes de rodar o sistema pela primeira vez, é preciso criar o banco SQLite com as tabelas. Há um script para cada sistema operacional, e **ambos devem ser executados a partir da pasta raiz do projeto**.

### Requisito

É necessário ter o cliente de linha de comando `sqlite3` instalado e disponível no `PATH`.

- **Linux (Debian/Ubuntu):** `sudo apt install sqlite3`
- **Windows:** `winget install SQLite.SQLite`

### Linux

```bash
./init_db_linux.sh
```

Se o script não tiver permissão de execução, rode antes `chmod +x init_db_linux.sh`.

### Windows

```bat
init_db_windows.bat
```

### Resultado

O script cria o arquivo `db.db` dentro da pasta `backend/`:

```
backend/db.db
```

Tabelas criadas: `Membros`, `Funcionarios`, `Treinos`, `Pagamentos`, `Historico_Atividades` e `Current_sessions`.
