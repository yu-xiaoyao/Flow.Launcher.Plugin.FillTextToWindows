using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Flow.Launcher.Plugin.FillTextToWindows.Data
{
    /// <summary>
    /// 保存填充记录的 SQLite 存储。数据量很小，每次操作开一个连接就够了（Microsoft.Data.Sqlite 自带连接池）。
    /// <para>
    /// 两张表：<c>FillEntries</c> 存记录本身，<c>FillEntryLines</c> 存那条记录的每一段数据，
    /// 用 <c>SortOrder</c> 记粘贴顺序。
    /// </para>
    /// </summary>
    public sealed class FillEntryStore
    {
        private const string DbDDL = """
                                     CREATE TABLE IF NOT EXISTS FillEntries (
                                         Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                                         Name              TEXT    NOT NULL,
                                         UseCustomSettings INTEGER NOT NULL DEFAULT 0,
                                         UseLineSettings   INTEGER NOT NULL DEFAULT 0,
                                         LeadingKeys       TEXT    NOT NULL DEFAULT '',
                                         NextFieldKeys     TEXT    NOT NULL DEFAULT '["Tab"]',
                                         LastFieldKeys     TEXT    NOT NULL DEFAULT '',
                                         PasteDelayMs      INTEGER NOT NULL DEFAULT 150,
                                         KeyDelayMs        INTEGER NOT NULL DEFAULT 40,
                                         RestoreClipboard  INTEGER NOT NULL DEFAULT 0
                                     );

                                     CREATE TABLE IF NOT EXISTS FillEntryLines (
                                         Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                                         EntryId       INTEGER NOT NULL REFERENCES FillEntries (Id) ON DELETE CASCADE,
                                         Value         TEXT    NOT NULL,
                                         LeadingKeys   TEXT    NOT NULL DEFAULT '',
                                         NextFieldKeys TEXT    NOT NULL DEFAULT '',
                                         LastFieldKeys TEXT    NOT NULL DEFAULT '',
                                         SortOrder     INTEGER NOT NULL
                                     );

                                     CREATE INDEX IF NOT EXISTS IX_FillEntryLines_EntryId
                                         ON FillEntryLines (EntryId, SortOrder);
                                     """;

        /// <summary>
        /// 后加的数据行列，老库的 <c>FillEntryLines</c> 里没有，启动时靠 <see cref="EnsureColumn"/> 补上。
        /// </summary>
        private static readonly (string Table, string Column, string Definition)[] AddedColumns =
        {
            ("FillEntries", "UseLineSettings", "INTEGER NOT NULL DEFAULT 0"),
            ("FillEntryLines", "LeadingKeys", "TEXT NOT NULL DEFAULT ''"),
            ("FillEntryLines", "NextFieldKeys", "TEXT NOT NULL DEFAULT ''"),
            ("FillEntryLines", "LastFieldKeys", "TEXT NOT NULL DEFAULT ''"),
        };

        /// <summary>
        /// 查记录时连行数据一起带出来，行按 <c>SortOrder</c> 排好序。
        /// <para>
        /// 行上的三个按键列必须起别名：和主表的同名列重名的话，<c>GetOrdinal</c> 读到的是主表那一列。
        /// </para>
        /// </summary>
        private const string SelectEntries =
            "SELECT e.Id, e.Name, e.UseCustomSettings, e.UseLineSettings, e.LeadingKeys, e.NextFieldKeys, " +
            "e.LastFieldKeys, e.PasteDelayMs, e.KeyDelayMs, e.RestoreClipboard, " +
            "l.Value, l.LeadingKeys AS LineLeadingKeys, l.NextFieldKeys AS LineNextFieldKeys, " +
            "l.LastFieldKeys AS LineLastFieldKeys " +
            "FROM FillEntries e LEFT JOIN FillEntryLines l ON l.EntryId = e.Id ";

        /// <summary>
        /// 按键配置存进 TEXT 列时的 JSON 写法。宽松转义：<c>+</c> 原样写，
        /// 不然默认编码器会把它转成 <c>+</c>，存进库里没人看得懂。
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly string _connectionString;        public FillEntryStore(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("数据库路径不能为空", nameof(databasePath));
            }

            DatabasePath = databasePath;
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString();
        }

        public string DatabasePath { get; }

        /// <summary>
        /// 建库建表，可以重复调用。
        /// </summary>
        /// <remarks>
        /// 老版本把多段数据以 JSON 挤在 <c>FillEntries.ValuesJson</c> 一列里。结构不一样了就直接重建，
        /// 不做数据迁移。
        /// </remarks>
        public void EnsureCreated()
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = Open();

            if (HasLegacyValuesColumn(connection))
            {
                // 先删从表，主表被外键引用着，顺序反了会删不掉
                Execute(connection, "DROP TABLE IF EXISTS FillEntryLines;");
                Execute(connection, "DROP TABLE IF EXISTS FillEntries;");
            }

            Execute(connection, DbDDL);

            // 老库升级。CREATE TABLE IF NOT EXISTS 对已经存在的表什么都不做，
            // 新加的列得自己补上，不然读的时候 GetOrdinal 会直接抛。
            foreach (var (table, column, definition) in AddedColumns)
            {
                EnsureColumn(connection, table, column, definition);
            }
        }

        /// <summary>
        /// 按名称搜索，匹配到的排前面，其余按名称排序。
        /// </summary>
        public List<FillEntry> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return GetAll();
            }

            using var connection = Open();
            using var command = connection.CreateCommand();

            // 关键字里的 % _ \ 都是 LIKE 的通配符，先转义掉，交给 SQL 过滤
            command.CommandText = SelectEntries
                                  + "WHERE e.Name LIKE @pattern ESCAPE '\\' ORDER BY e.Id, l.SortOrder;";
            command.Parameters.AddWithValue("@pattern", "%" + EscapeLike(keyword.Trim()) + "%");

            var entries = ReadEntries(command);

            return entries
                .OrderByDescending(entry => MatchScore(entry.Name, keyword.Trim()))
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<FillEntry> GetAll()
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = SelectEntries + "ORDER BY e.Id, l.SortOrder;";

            return ReadEntries(command)
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 新增或更新一条记录（含它的所有行数据），回填 <see cref="FillEntry.Id"/>。
        /// </summary>
        public void Save(FillEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var custom = entry.CustomSettings ?? new Settings();

            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            long entryId;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.Parameters.AddWithValue("@name", entry.Name ?? string.Empty);
                command.Parameters.AddWithValue("@useCustom", entry.UseCustomSettings ? 1 : 0);
                command.Parameters.AddWithValue("@useLine", entry.UseLineSettings ? 1 : 0);
                command.Parameters.AddWithValue("@leading", ToJsonArray(custom.LeadingKeys));
                command.Parameters.AddWithValue("@next", ToJsonArray(custom.NextFieldKeys));
                command.Parameters.AddWithValue("@last", ToJsonArray(custom.LastFieldKeys));
                command.Parameters.AddWithValue("@pasteDelay", custom.PasteDelayMs);
                command.Parameters.AddWithValue("@keyDelay", custom.KeyDelayMs);
                command.Parameters.AddWithValue("@restoreClipboard", custom.RestoreClipboard ? 1 : 0);

                if (entry.Id > 0)
                {
                    command.CommandText =
                        """
                        UPDATE FillEntries SET
                            Name = @name,
                            UseCustomSettings = @useCustom,
                            UseLineSettings = @useLine,
                            LeadingKeys = @leading,
                            NextFieldKeys = @next,
                            LastFieldKeys = @last,
                            PasteDelayMs = @pasteDelay,
                            KeyDelayMs = @keyDelay,
                            RestoreClipboard = @restoreClipboard
                        WHERE Id = @id;
                        """;
                    command.Parameters.AddWithValue("@id", entry.Id);
                    command.ExecuteNonQuery();

                    entryId = entry.Id;
                }
                else
                {
                    command.CommandText =
                        """
                        INSERT INTO FillEntries
                            (Name, UseCustomSettings, UseLineSettings, LeadingKeys, NextFieldKeys, LastFieldKeys,
                             PasteDelayMs, KeyDelayMs, RestoreClipboard)
                        VALUES
                            (@name, @useCustom, @useLine, @leading, @next, @last,
                             @pasteDelay, @keyDelay, @restoreClipboard);
                        SELECT last_insert_rowid();
                        """;

                    entryId = Convert.ToInt64(command.ExecuteScalar());
                    entry.Id = entryId;
                }
            }

            // 行数据整体替换，省得去做逐行 diff
            DeleteLines(connection, transaction, entryId);
            InsertLines(connection, transaction, entryId, entry.Values);

            transaction.Commit();
        }

        public void Delete(long id)
        {
            if (id <= 0)
            {
                return;
            }

            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            // 外键是 ON DELETE CASCADE，这里显式删一遍，免得有人把 pragma 关掉后留下孤儿行
            DeleteLines(connection, transaction, id);

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM FillEntries WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // ON DELETE CASCADE 要靠这个才生效，而且它是连接级的，所以每次开连接都得设
            Execute(connection, "PRAGMA foreign_keys = ON;");

            return connection;
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static void DeleteLines(SqliteConnection connection, SqliteTransaction transaction, long entryId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM FillEntryLines WHERE EntryId = @entryId;";
            command.Parameters.AddWithValue("@entryId", entryId);
            command.ExecuteNonQuery();
        }

        private static void InsertLines(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long entryId,
            IReadOnlyList<FillEntryLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO FillEntryLines (EntryId, Value, LeadingKeys, NextFieldKeys, LastFieldKeys, SortOrder)
                VALUES (@entryId, @value, @leading, @next, @last, @sortOrder);
                """;

            var entryIdParameter = command.Parameters.Add("@entryId", SqliteType.Integer);
            var valueParameter = command.Parameters.Add("@value", SqliteType.Text);
            var leadingParameter = command.Parameters.Add("@leading", SqliteType.Text);
            var nextParameter = command.Parameters.Add("@next", SqliteType.Text);
            var lastParameter = command.Parameters.Add("@last", SqliteType.Text);
            var sortOrderParameter = command.Parameters.Add("@sortOrder", SqliteType.Integer);

            entryIdParameter.Value = entryId;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? new FillEntryLine();

                valueParameter.Value = line.Value ?? string.Empty;
                leadingParameter.Value = ToJsonArray(line.LeadingKeys) ?? "[]";
                nextParameter.Value = ToJsonArray(line.NextFieldKeys) ?? "[]";
                lastParameter.Value = ToJsonArray(line.LastFieldKeys) ?? "[]";
                sortOrderParameter.Value = i + 1; // 排序号从 1 开始
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 按键配置存成 JSON 数组，例如 <c>["Tab"]</c>、<c>["Ctrl+A", "Delete"]</c>。
        /// </summary>
        /// <remarks>
        /// 用宽松转义，<c>+</c> 按原样写进库里，而不是被转成 <c>+</c> —— 这段文本是要给人看的。
        /// </remarks>
        public static string ToJsonArray(List<string> list)
        {
            if (list == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(list, SerializerOptions);
        }

        /// <summary>
        /// 读按键配置。正常情况就是一个 JSON 数组，读到别的（老版本写进去的整串、手改坏了的）
        /// 也按逗号拆开当数组用，别让一条脏数据把整个列表读崩。
        /// </summary>
        public static List<string> ParseJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            var text = json.Trim();

            if (text[0] != '[')
            {
                return text
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .ToList();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(text) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 一次查询把记录和行数据都读出来，按 <c>e.Id</c> 分组（SQL 里已经按 Id + SortOrder 排好）。
        /// </summary>
        /// <remarks>
        /// 下标按列名现查，别写死序号：<see cref="SelectEntries"/> 里增删一列，写死的序号就会读错位。
        /// </remarks>
        private static List<FillEntry> ReadEntries(SqliteCommand command)
        {
            var entries = new List<FillEntry>();
            FillEntry current = null;

            using var reader = command.ExecuteReader();

            var idColumn = reader.GetOrdinal("Id");
            var nameColumn = reader.GetOrdinal("Name");
            var useCustomColumn = reader.GetOrdinal("UseCustomSettings");
            var useLineColumn = reader.GetOrdinal("UseLineSettings");
            var leadingColumn = reader.GetOrdinal("LeadingKeys");
            var nextColumn = reader.GetOrdinal("NextFieldKeys");
            var lastColumn = reader.GetOrdinal("LastFieldKeys");
            var pasteDelayColumn = reader.GetOrdinal("PasteDelayMs");
            var keyDelayColumn = reader.GetOrdinal("KeyDelayMs");
            var restoreClipboardColumn = reader.GetOrdinal("RestoreClipboard");
            var valueColumn = reader.GetOrdinal("Value");
            var lineLeadingColumn = reader.GetOrdinal("LineLeadingKeys");
            var lineNextColumn = reader.GetOrdinal("LineNextFieldKeys");
            var lineLastColumn = reader.GetOrdinal("LineLastFieldKeys");

            while (reader.Read())
            {
                var id = reader.GetInt64(idColumn);

                if (current == null || current.Id != id)
                {
                    current = new FillEntry
                    {
                        Id = id,
                        Name = reader.GetString(nameColumn),
                        UseCustomSettings = reader.GetInt64(useCustomColumn) != 0,
                        UseLineSettings = reader.GetInt64(useLineColumn) != 0,
                        CustomSettings = new Settings
                        {
                            LeadingKeys = ReadKeys(reader, leadingColumn),
                            NextFieldKeys = ReadKeys(reader, nextColumn),
                            LastFieldKeys = ReadKeys(reader, lastColumn),
                            PasteDelayMs = (int)reader.GetInt64(pasteDelayColumn),
                            KeyDelayMs = (int)reader.GetInt64(keyDelayColumn),
                            RestoreClipboard = reader.GetInt64(restoreClipboardColumn) != 0,
                        },
                    };

                    entries.Add(current);
                }

                // LEFT JOIN：没有行数据的记录这里会是 NULL
                if (!reader.IsDBNull(valueColumn))
                {
                    current.Values.Add(new FillEntryLine
                    {
                        Value = reader.GetString(valueColumn),
                        LeadingKeys = ReadKeys(reader, lineLeadingColumn),
                        NextFieldKeys = ReadKeys(reader, lineNextColumn),
                        LastFieldKeys = ReadKeys(reader, lineLastColumn),
                    });
                }
            }

            return entries;
        }

        private static List<string> ReadKeys(SqliteDataReader reader, int column)
        {
            return reader.IsDBNull(column) ? new List<string>() : ParseJsonArray(reader.GetString(column));
        }

        /// <summary>
        /// 表里没有这一列就加上。加列的时候 SQLite 要求 <c>NOT NULL</c> 必须带默认值，
        /// <see cref="AddedColumns"/> 里那几条定义都给了，所以直接 ALTER 就行。
        /// </summary>
        private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @column;";
                command.Parameters.AddWithValue("@column", column);

                if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                {
                    return;
                }
            }

            Execute(connection, $"ALTER TABLE {table} ADD COLUMN {column} {definition};");
        }

        /// <summary>
        /// 老版本把多段数据以 JSON 存在 FillEntries.ValuesJson 里，靠这一列判断要不要重建表。
        /// </summary>
        private static bool HasLegacyValuesColumn(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('FillEntries') WHERE name = 'ValuesJson';";

            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 完全一致 3 分、以关键字开头 2 分、只是包含 1 分。
        /// </summary>
        private static int MatchScore(string name, string keyword)
        {
            if (string.Equals(name, keyword, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 1;
        }

        private static string EscapeLike(string keyword)
        {
            return keyword
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }
    }
}