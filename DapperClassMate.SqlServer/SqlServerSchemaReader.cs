using DapperClassMate.Core;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DapperClassMate.SqlServer
{
    public sealed class SqlServerSchemaReader
    {
        public async Task<IReadOnlyList<SqlParameterInfo>> GetStoredProcedureParametersAsync(
            string connectionString,
            string storedProcedureName)
        {
            const string sql = @"
            SELECT
                p.name AS ParameterName,
                t.name AS SqlType,
                p.is_output AS IsOutput,
                p.max_length AS MaxLength,
                p.precision AS Precision,
                p.scale AS Scale
            FROM sys.parameters p
            INNER JOIN sys.types t
                ON p.user_type_id = t.user_type_id
            WHERE p.object_id = OBJECT_ID(@StoredProcedureName)
            ORDER BY p.parameter_id;";

            var results = new List<SqlParameterInfo>();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@StoredProcedureName", storedProcedureName);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SqlParameterInfo
                        {
                            Name = reader["ParameterName"].ToString(),
                            SqlType = reader["SqlType"].ToString(),
                            IsOutput = (bool)reader["IsOutput"],
                            MaxLength = (short)reader["MaxLength"],
                            Precision = (byte)reader["Precision"],
                            Scale = (byte)reader["Scale"],
                            IsNullable = true
                        });
                    }
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<SqlColumnInfo>> GetStoredProcedureResultColumnsAsync(string connectionString, string storedProcedureName)
        {
            var describeColumns = await TryGetResultColumnsFromDescribeAsync(
                connectionString,
                storedProcedureName);

            if (describeColumns.Count > 0)
                return describeColumns;

            return await GetResultColumnsFromExecutionFallbackAsync(
                connectionString,
                storedProcedureName);
        }

        private async Task<IReadOnlyList<SqlColumnInfo>> TryGetResultColumnsFromDescribeAsync(
            string connectionString,
            string storedProcedureName)
        {
            var results = new List<SqlColumnInfo>();

            const string sql = @"
            DECLARE @tsql nvarchar(max) = N'EXEC ' + @StoredProcedureName;

            EXEC sys.sp_describe_first_result_set
                @tsql = @tsql,
                @params = NULL,
                @browse_information_mode = 0;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StoredProcedureName", storedProcedureName);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var errorNumber = reader["error_number"];

                            if (errorNumber != DBNull.Value)
                                continue;

                            results.Add(new SqlColumnInfo
                            {
                                Name = reader["name"].ToString(),
                                SqlType = reader["system_type_name"].ToString(),
                                IsNullable = reader["is_nullable"] != DBNull.Value &&
                                             Convert.ToBoolean(reader["is_nullable"])
                            });
                        }
                    }
                }
            }
            catch
            {
                // Intentionally fall back to execution-based schema detection.
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlColumnInfo>> GetResultColumnsFromExecutionFallbackAsync(
            string connectionString,
            string storedProcedureName)
        {
            var results = new List<SqlColumnInfo>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand(storedProcedureName, connection, transaction))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    var parameters = await GetStoredProcedureParametersAsync(
                        connectionString,
                        storedProcedureName);

                    foreach (var parameter in parameters)
                    {
                        var sqlParameter = command.Parameters.Add(
                            parameter.Name,
                            GetSqlDbType(parameter.SqlType));

                        sqlParameter.Value = GetDefaultValue(parameter.SqlType);
                    }

                    try
                    {
                        using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly))
                        {
                            var schemaTable = reader.GetSchemaTable();

                            if (schemaTable != null)
                            {
                                foreach (DataRow row in schemaTable.Rows)
                                {
                                    results.Add(new SqlColumnInfo
                                    {
                                        Name = row["ColumnName"].ToString(),
                                        SqlType = GetSqlTypeNameFromClrType(row["DataType"] as Type),
                                        IsNullable = row.Table.Columns.Contains("AllowDBNull") &&
                                                     row["AllowDBNull"] != DBNull.Value &&
                                                     Convert.ToBoolean(row["AllowDBNull"])
                                    });
                                }
                            }
                        }
                    }
                    finally
                    {
                        transaction.Rollback();
                    }
                }
            }

            return results;
        }

        private static SqlDbType GetSqlDbType(string sqlType)
        {
            switch (sqlType.ToLowerInvariant())
            {
                case "bigint": return SqlDbType.BigInt;
                case "binary": return SqlDbType.Binary;
                case "bit": return SqlDbType.Bit;
                case "char": return SqlDbType.Char;
                case "date": return SqlDbType.Date;
                case "datetime": return SqlDbType.DateTime;
                case "datetime2": return SqlDbType.DateTime2;
                case "datetimeoffset": return SqlDbType.DateTimeOffset;
                case "decimal": return SqlDbType.Decimal;
                case "float": return SqlDbType.Float;
                case "image": return SqlDbType.Image;
                case "int": return SqlDbType.Int;
                case "money": return SqlDbType.Money;
                case "nchar": return SqlDbType.NChar;
                case "ntext": return SqlDbType.NText;
                case "numeric": return SqlDbType.Decimal;
                case "nvarchar": return SqlDbType.NVarChar;
                case "real": return SqlDbType.Real;
                case "smalldatetime": return SqlDbType.SmallDateTime;
                case "smallint": return SqlDbType.SmallInt;
                case "smallmoney": return SqlDbType.SmallMoney;
                case "text": return SqlDbType.Text;
                case "time": return SqlDbType.Time;
                case "timestamp": return SqlDbType.Timestamp;
                case "tinyint": return SqlDbType.TinyInt;
                case "uniqueidentifier": return SqlDbType.UniqueIdentifier;
                case "varbinary": return SqlDbType.VarBinary;
                case "varchar": return SqlDbType.VarChar;
                case "xml": return SqlDbType.Xml;
                default: return SqlDbType.Variant;
            }
        }

        private static object GetDefaultValue(string sqlType)
        {
            switch (sqlType.ToLowerInvariant())
            {
                case "bigint": return 0L;
                case "int": return 0;
                case "smallint": return (short)0;
                case "tinyint": return (byte)0;
                case "bit": return false;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": return 0m;
                case "float": return 0d;
                case "real": return 0f;
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime": return DateTime.Today;
                case "datetimeoffset": return DateTimeOffset.Now;
                case "time": return TimeSpan.Zero;
                case "uniqueidentifier": return Guid.Empty;
                case "binary":
                case "varbinary":
                case "image": return new byte[0];
                default: return string.Empty;
            }
        }

        private static string GetSqlTypeNameFromClrType(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "bigint";
            if (type == typeof(short)) return "smallint";
            if (type == typeof(byte)) return "tinyint";
            if (type == typeof(bool)) return "bit";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(double)) return "float";
            if (type == typeof(float)) return "real";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(DateTimeOffset)) return "datetimeoffset";
            if (type == typeof(TimeSpan)) return "time";
            if (type == typeof(Guid)) return "uniqueidentifier";
            if (type == typeof(byte[])) return "varbinary";

            return "nvarchar";
        }
        public async Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString)
        {
            const string sql = @"
            SELECT 
                QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name) AS ProcedureName
            FROM sys.procedures
            WHERE is_ms_shipped = 0
            ORDER BY SCHEMA_NAME(schema_id), name;";

            var results = new List<string>();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(reader["ProcedureName"].ToString());
                    }
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<string>> GetTablesAsync(string connectionString)
        {
            const string sql = @"
            SELECT
                TABLE_SCHEMA + '.' + TABLE_NAME AS ObjectName
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;";

            return await GetSchemaObjectsAsync(connectionString, sql);
        }

        public async Task<IReadOnlyList<string>> GetViewsAsync(string connectionString)
        {
            const string sql = @"
            SELECT
                TABLE_SCHEMA + '.' + TABLE_NAME AS ObjectName
            FROM INFORMATION_SCHEMA.VIEWS
            ORDER BY TABLE_SCHEMA, TABLE_NAME;";

            return await GetSchemaObjectsAsync(connectionString, sql);
        }

        public async Task<IReadOnlyList<SqlColumnInfo>> GetObjectColumnsAsync(
            string connectionString,
            string objectName)
        {
            const string sql = @"
            SELECT
                c.name AS ColumnName,
                t.name AS SqlType,
                c.is_nullable AS IsNullable,
                CASE WHEN pk.ColumnName IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsPrimaryKey,
                c.is_identity AS IsIdentity,
                c.is_computed AS IsComputed
            FROM sys.objects o
            INNER JOIN sys.schemas s
                ON o.schema_id = s.schema_id
            INNER JOIN sys.columns c
                ON o.object_id = c.object_id
            INNER JOIN sys.types t
                ON c.user_type_id = t.user_type_id
            LEFT JOIN
            (
                SELECT
                    ic.object_id,
                    col.name AS ColumnName
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic
                    ON i.object_id = ic.object_id
                   AND i.index_id = ic.index_id
                INNER JOIN sys.columns col
                    ON ic.object_id = col.object_id
                   AND ic.column_id = col.column_id
                WHERE i.is_primary_key = 1
            ) pk
                ON c.object_id = pk.object_id
               AND c.name = pk.ColumnName
            WHERE s.name + '.' + o.name = @ObjectName
               OR QUOTENAME(s.name) + '.' + QUOTENAME(o.name) = @ObjectName
            ORDER BY c.column_id;";

            var results = new List<SqlColumnInfo>();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ObjectName", objectName);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SqlColumnInfo
                        {
                            Name = reader["ColumnName"].ToString(),
                            SqlType = reader["SqlType"].ToString(),
                            IsNullable = reader["IsNullable"] != DBNull.Value &&
                                         Convert.ToBoolean(reader["IsNullable"]),
                            IsPrimaryKey = reader["IsPrimaryKey"] != DBNull.Value &&
                                           Convert.ToBoolean(reader["IsPrimaryKey"]),
                            IsIdentity = reader["IsIdentity"] != DBNull.Value &&
                                         Convert.ToBoolean(reader["IsIdentity"]),
                            IsComputed = reader["IsComputed"] != DBNull.Value &&
                                         Convert.ToBoolean(reader["IsComputed"])
                        });
                    }
                }
            }

            return results;
        }

        private static async Task<IReadOnlyList<string>> GetSchemaObjectsAsync(
            string connectionString,
            string sql)
        {
            var results = new List<string>();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(reader["ObjectName"].ToString());
                    }
                }
            }

            return results;
        }
    }
}
