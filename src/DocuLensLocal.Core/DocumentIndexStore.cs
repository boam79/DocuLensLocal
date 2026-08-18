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
                status TEXT NOT NULL
            );
            """;
        create.ExecuteNonQuery();
    }

    public void Upsert(IndexedDocument document)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO documents (path, size_bytes, last_write_time_utc, indexed_at_utc, status)
            VALUES ($path, $size, $mtime, $indexed, $status)
            ON CONFLICT(path) DO UPDATE SET
                size_bytes = excluded.size_bytes,
                last_write_time_utc = excluded.last_write_time_utc,
                indexed_at_utc = excluded.indexed_at_utc,
                status = excluded.status;
            """;
        cmd.Parameters.AddWithValue("$path", document.FilePath);
        cmd.Parameters.AddWithValue("$size", document.SizeBytes);
        cmd.Parameters.AddWithValue("$mtime", document.LastWriteTimeUtc.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$indexed", document.IndexedAtUtc.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$status", document.Status);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<IndexedDocument> GetAll() => Query("SELECT path, size_bytes, last_write_time_utc, indexed_at_utc, status FROM documents ORDER BY path");

    public IReadOnlyList<IndexedDocument> SearchByFileName(string query)
    {
        var tokens = FilenameSearchQuery.ExtractTokens(query);
        if (tokens.Count == 0)
        {
            return SearchPathLike([query], requireAll: true);
        }

        var andHits = SearchPathLike(tokens, requireAll: true);
        if (andHits.Count > 0 || tokens.Count == 1)
        {
            return andHits;
        }

        return SearchPathLike(tokens, requireAll: false);
    }

    private IReadOnlyList<IndexedDocument> SearchPathLike(IReadOnlyList<string> tokens, bool requireAll)
    {
        using var cmd = _connection.CreateCommand();
        var clauses = new List<string>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            var name = "$t" + i;
            clauses.Add($"path LIKE {name} ESCAPE '\\'");
            cmd.Parameters.AddWithValue(name, "%" + EscapeLike(tokens[i]) + "%");
        }

        var joiner = requireAll ? " AND " : " OR ";
        cmd.CommandText = $"""
            SELECT path, size_bytes, last_write_time_utc, indexed_at_utc, status
            FROM documents
            WHERE {string.Join(joiner, clauses)}
            ORDER BY path;
            """;
        return ReadAll(cmd);
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

    private IReadOnlyList<IndexedDocument> Query(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return ReadAll(cmd);
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
            });
        }

        return results;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
