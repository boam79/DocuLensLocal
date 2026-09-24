using Microsoft.Data.Sqlite;

namespace DocuLensLocal.Core;

internal sealed class DocumentIndexStore : IDisposable
{
    private readonly SqliteConnection _connection;

    public DocumentIndexStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString());
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            """;
        pragma.ExecuteNonQuery();

        using var create = _connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS documents (
                path TEXT PRIMARY KEY COLLATE NOCASE,
                size_bytes INTEGER NOT NULL,
                last_write_time_utc TEXT NOT NULL,
                indexed_at_utc TEXT NOT NULL,
                status TEXT NOT NULL,
                body_text TEXT NOT NULL DEFAULT '',
                page_count INTEGER NOT NULL DEFAULT 0,
                ocr_page_count INTEGER NOT NULL DEFAULT 0
            );
            """;
        create.ExecuteNonQuery();
        EnsureColumn("body_text", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn("page_count", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("ocr_page_count", "INTEGER NOT NULL DEFAULT 0");
    }

    public void Upsert(IndexedDocument document)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO documents (path, size_bytes, last_write_time_utc, indexed_at_utc, status, body_text, page_count, ocr_page_count)
            VALUES ($path, $size, $mtime, $indexed, $status, $body, $pages, $ocr)
            ON CONFLICT(path) DO UPDATE SET
                size_bytes = excluded.size_bytes,
                last_write_time_utc = excluded.last_write_time_utc,
                indexed_at_utc = excluded.indexed_at_utc,
                status = excluded.status,
                body_text = excluded.body_text,
                page_count = excluded.page_count,
                ocr_page_count = excluded.ocr_page_count;
            """;
        cmd.Parameters.AddWithValue("$path", document.FilePath);
        cmd.Parameters.AddWithValue("$size", document.SizeBytes);
        cmd.Parameters.AddWithValue("$mtime", document.LastWriteTimeUtc.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$indexed", document.IndexedAtUtc.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$status", document.Status);
        cmd.Parameters.AddWithValue("$body", document.BodyText);
        cmd.Parameters.AddWithValue("$pages", document.PageCount);
        cmd.Parameters.AddWithValue("$ocr", document.OcrPageCount);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<IndexedDocument> GetAll() =>
        Query("SELECT path, size_bytes, last_write_time_utc, indexed_at_utc, status, body_text, page_count, ocr_page_count FROM documents ORDER BY path");

    public IReadOnlyList<SearchHit> Search(string query)
    {
        var tokens = FilenameSearchQuery.ExtractTokens(query);
        if (tokens.Count == 0)
        {
            tokens = string.IsNullOrWhiteSpace(query) ? [] : [query.Trim()];
        }

        if (tokens.Count == 0)
        {
            return [];
        }

        var andHits = SearchTokens(tokens, requireAll: true);
        var rows = andHits.Count > 0 || tokens.Count == 1
            ? andHits
            : SearchTokens(tokens, requireAll: false);

        return rows.Select(doc => ToHit(doc, tokens)).ToList();
    }

    public IReadOnlyList<IndexedDocument> SearchByFileName(string query) =>
        Search(query).Select(hit => hit.Document).ToList();

    public int DeleteAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM documents;";
        return cmd.ExecuteNonQuery();
    }

    public void KeepOnly(IReadOnlyCollection<string> paths)
    {
        using var tx = _connection.BeginTransaction();
        using (var create = _connection.CreateCommand())
        {
            create.Transaction = tx;
            create.CommandText = "CREATE TEMP TABLE IF NOT EXISTS keep_paths (path TEXT PRIMARY KEY COLLATE NOCASE);";
            create.ExecuteNonQuery();
        }

        using (var clear = _connection.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM keep_paths;";
            clear.ExecuteNonQuery();
        }

        using (var insert = _connection.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = "INSERT OR IGNORE INTO keep_paths(path) VALUES ($path);";
            var parameter = insert.CreateParameter();
            parameter.ParameterName = "$path";
            insert.Parameters.Add(parameter);
            foreach (var path in paths)
            {
                parameter.Value = path;
                insert.ExecuteNonQuery();
            }
        }

        using (var delete = _connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM documents WHERE path NOT IN (SELECT path FROM keep_paths);";
            delete.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void Dispose()
    {
        try
        {
            using var checkpoint = _connection.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpoint.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
        }

        _connection.Dispose();
        SqliteConnection.ClearAllPools();
    }

    private IReadOnlyList<IndexedDocument> SearchTokens(IReadOnlyList<string> tokens, bool requireAll)
    {
        using var cmd = _connection.CreateCommand();
        var clauses = new List<string>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            var pathName = "$p" + i;
            var bodyName = "$b" + i;
            var yearName = "$y" + i;
            var like = "%" + EscapeLike(tokens[i]) + "%";
            var yearClause = IsYearToken(tokens[i])
                ? $" OR last_write_time_utc LIKE {yearName}"
                : string.Empty;
            if (IsYearToken(tokens[i]))
            {
                cmd.Parameters.AddWithValue(yearName, "%" + EscapeLike(tokens[i]) + "%");
            }

            clauses.Add($"(path LIKE {pathName} ESCAPE '\\' OR body_text LIKE {bodyName} ESCAPE '\\'{yearClause})");
            cmd.Parameters.AddWithValue(pathName, like);
            cmd.Parameters.AddWithValue(bodyName, like);
        }

        var joiner = requireAll ? " AND " : " OR ";
        cmd.CommandText = $"""
            SELECT path, size_bytes, last_write_time_utc, indexed_at_utc, status, body_text, page_count, ocr_page_count
            FROM documents
            WHERE {string.Join(joiner, clauses)}
            ORDER BY path;
            """;
        return ReadAll(cmd);
    }

    private IReadOnlyList<IndexedDocument> Query(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return ReadAll(cmd);
    }

    private static SearchHit ToHit(IndexedDocument document, IReadOnlyList<string> tokens)
    {
        var pathMatch = TokensMatch(document.FilePath, tokens);
        var bodyMatch = TokensMatch(document.BodyText, tokens);
        var kind = pathMatch && bodyMatch
            ? SearchMatchKind.Both
            : pathMatch
                ? SearchMatchKind.FileName
                : SearchMatchKind.Body;
        var snippet = bodyMatch
            ? EvidenceSnippet.From(document.BodyText, tokens)
            : string.Empty;
        return new SearchHit
        {
            Document = document,
            MatchKind = kind,
            Snippet = snippet,
            MatchLabelKo = FormatLabel(kind, document.OcrPageCount),
        };
    }

    private static bool TokensMatch(string? haystack, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        return tokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatLabel(SearchMatchKind kind, int ocrPageCount)
    {
        var label = kind switch
        {
            SearchMatchKind.Both => "파일명·본문",
            SearchMatchKind.Body => "본문",
            _ => "파일명",
        };
        if (ocrPageCount > 0 && kind != SearchMatchKind.FileName)
        {
            label += " · OCR";
        }

        return label;
    }

    private static bool IsYearToken(string token) =>
        token.Length == 4 && token.All(char.IsDigit) && token.StartsWith("20", StringComparison.Ordinal);

    private void EnsureColumn(string name, string definition)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(documents);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        reader.Close();
        using var alter = _connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE documents ADD COLUMN {name} {definition};";
        alter.ExecuteNonQuery();
    }

    private static List<IndexedDocument> ReadAll(SqliteCommand cmd)
    {
        var results = new List<IndexedDocument>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new IndexedDocument
            {
                FilePath = reader.GetString(0),
                SizeBytes = reader.GetInt64(1),
                LastWriteTimeUtc = DateTimeOffset.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                IndexedAtUtc = DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Status = reader.GetString(4),
                BodyText = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : string.Empty,
                PageCount = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetInt32(6) : 0,
                OcrPageCount = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetInt32(7) : 0,
            });
        }

        return results;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
