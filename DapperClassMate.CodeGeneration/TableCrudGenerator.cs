using DapperClassMate.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DapperClassMate.CodeGeneration
{
    public sealed class TableCrudGenerator
    {
        public string Generate(
            string objectName,
            string modelClassName,
            string methodBaseName,
            IReadOnlyList<SqlColumnInfo> columns,
            CrudOperation operations,
            int commandTimeout = 30,
            string resultSetCollectionType = "IReadOnlyList")
        {
            var sb = new StringBuilder();

            if ((operations & CrudOperation.Create) == CrudOperation.Create)
            {
                sb.AppendLine(GenerateCreateMethod(objectName, modelClassName, methodBaseName, columns, commandTimeout));
            }

            if ((operations & CrudOperation.Read) == CrudOperation.Read)
            {
                sb.AppendLine(GenerateReadMethod(objectName, modelClassName, methodBaseName, commandTimeout, resultSetCollectionType));
            }

            if ((operations & CrudOperation.Update) == CrudOperation.Update)
            {
                sb.AppendLine(GenerateUpdateMethod(objectName, modelClassName, methodBaseName, columns, commandTimeout));
            }

            if ((operations & CrudOperation.Delete) == CrudOperation.Delete)
            {
                sb.AppendLine(GenerateDeleteMethod(objectName, methodBaseName, columns, commandTimeout));
            }

            return sb.ToString();
        }

        private static string GenerateCreateMethod(
            string objectName,
            string modelClassName,
            string methodBaseName,
            IReadOnlyList<SqlColumnInfo> columns,
            int commandTimeout)
        {
            var insertColumns = columns
                .Where(c => !c.IsIdentity && !c.IsComputed)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"public async Task<int> Insert{methodBaseName}Async(");
            sb.AppendLine($"    {modelClassName} item,");
            sb.AppendLine("    CancellationToken cancellationToken = default)");
            sb.AppendLine("{");
            sb.AppendLine("    using (var connection = new SqlConnection([insert connection string here])");
            sb.AppendLine("    {");
            sb.AppendLine("        var sql = @\"");
            sb.AppendLine("INSERT INTO " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("(" + string.Join(", ", insertColumns.Select(c => QuoteSqlIdentifier(c.Name))) + ")");
            sb.AppendLine("VALUES");
            sb.AppendLine("(" + string.Join(", ", insertColumns.Select(c => "@" + ToPascalCase(c.Name))) + ");\";");
            sb.AppendLine();
            sb.AppendLine("        return await connection.ExecuteAsync(");
            sb.AppendLine("            new CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                item,");
            sb.AppendLine("                commandType: CommandType.Text,");
            sb.AppendLine($"                commandTimeout: {commandTimeout},");
            sb.AppendLine("                cancellationToken: cancellationToken));");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateReadMethod(
            string objectName,
            string modelClassName,
            string methodBaseName,
            int commandTimeout,
            string resultSetCollectionType)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"public async Task<{resultSetCollectionType}<{modelClassName}>> Get{methodBaseName}Async(");
            sb.AppendLine("    CancellationToken cancellationToken = default)");
            sb.AppendLine("{");
            sb.AppendLine("    using (var connection = new SqlConnection([insert connection string here])");
            sb.AppendLine("    {");
            sb.AppendLine($"        var results = await connection.QueryAsync<{modelClassName}>(");
            sb.AppendLine("            new CommandDefinition(");
            sb.AppendLine($"                \"SELECT * FROM {QuoteSchemaQualifiedName(objectName)}\",");
            sb.AppendLine("                commandType: CommandType.Text,");
            sb.AppendLine($"                commandTimeout: {commandTimeout},");
            sb.AppendLine("                cancellationToken: cancellationToken));");
            sb.AppendLine();
            sb.AppendLine("        return results.AsList();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateUpdateMethod(
            string objectName,
            string modelClassName,
            string methodBaseName,
            IReadOnlyList<SqlColumnInfo> columns,
            int commandTimeout)
        {
            var keyColumns = columns.Where(c => c.IsPrimaryKey).ToList();
            var updateColumns = columns
                .Where(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"public async Task<int> Update{methodBaseName}Async(");
            sb.AppendLine($"    {modelClassName} item,");
            sb.AppendLine("    CancellationToken cancellationToken = default)");
            sb.AppendLine("{");
            sb.AppendLine("    using (var connection = new SqlConnection([insert connection string here])");
            sb.AppendLine("    {");
            sb.AppendLine("        var sql = @\"");
            sb.AppendLine("UPDATE " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("SET");
            sb.AppendLine(string.Join("," + Environment.NewLine, updateColumns.Select(c =>
                "    " + QuoteSqlIdentifier(c.Name) + " = @" + ToPascalCase(c.Name))));
            sb.AppendLine("WHERE");
            sb.AppendLine(string.Join(Environment.NewLine + "AND ", keyColumns.Select(c =>
                QuoteSqlIdentifier(c.Name) + " = @" + ToPascalCase(c.Name))) + ";\";");
            sb.AppendLine();
            sb.AppendLine("        return await connection.ExecuteAsync(");
            sb.AppendLine("            new CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                item,");
            sb.AppendLine("                commandType: CommandType.Text,");
            sb.AppendLine($"                commandTimeout: {commandTimeout},");
            sb.AppendLine("                cancellationToken: cancellationToken));");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateDeleteMethod(
            string objectName,
            string methodBaseName,
            IReadOnlyList<SqlColumnInfo> columns,
            int commandTimeout)
        {
            var keyColumns = columns.Where(c => c.IsPrimaryKey).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"public async Task<int> Delete{methodBaseName}Async(");

            foreach (var keyColumn in keyColumns)
            {
                sb.AppendLine($"    {ToCSharpType(keyColumn.SqlType, keyColumn.IsNullable)} {ToCamelCase(keyColumn.Name)},");
            }

            sb.AppendLine("    CancellationToken cancellationToken = default)");
            sb.AppendLine("{");
            sb.AppendLine("    using (var connection = new SqlConnection([insert connection string here])");
            sb.AppendLine("    {");
            sb.AppendLine("        var sql = @\"");
            sb.AppendLine("DELETE FROM " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("WHERE");
            sb.AppendLine(string.Join(Environment.NewLine + "AND ", keyColumns.Select(c =>
                QuoteSqlIdentifier(c.Name) + " = @" + ToCamelCase(c.Name))) + ";\";");
            sb.AppendLine();
            sb.AppendLine("        return await connection.ExecuteAsync(");
            sb.AppendLine("            new CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                new");
            sb.AppendLine("                {");

            foreach (var keyColumn in keyColumns)
            {
                sb.AppendLine($"                    {ToCamelCase(keyColumn.Name)},");
            }

            sb.AppendLine("                },");
            sb.AppendLine("                commandType: CommandType.Text,");
            sb.AppendLine($"                commandTimeout: {commandTimeout},");
            sb.AppendLine("                cancellationToken: cancellationToken));");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string QuoteSchemaQualifiedName(string objectName)
        {
            var cleanName = (objectName ?? string.Empty)
                .Trim()
                .Replace("[", string.Empty)
                .Replace("]", string.Empty);

            return string.Join(".", cleanName
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => QuoteSqlIdentifier(part.Trim())));
        }

        private static string QuoteSqlIdentifier(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var parts = value
                .Replace("-", "_")
                .Replace(" ", "_")
                .Split('_')
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Concat(parts.Select(p =>
                char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        private static string ToCamelCase(string value)
        {
            var pascal = ToPascalCase(value);

            if (string.IsNullOrWhiteSpace(pascal))
            {
                return pascal;
            }

            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        private static string ToCSharpType(string sqlType, bool nullable)
        {
            var cleanSqlType = (sqlType ?? string.Empty).ToLowerInvariant();
            var parenIndex = cleanSqlType.IndexOf('(');
            if (parenIndex >= 0)
            {
                cleanSqlType = cleanSqlType.Substring(0, parenIndex);
            }

            string type;

            switch (cleanSqlType)
            {
                case "int": type = "int"; break;
                case "bigint": type = "long"; break;
                case "smallint": type = "short"; break;
                case "tinyint": type = "byte"; break;
                case "bit": type = "bool"; break;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": type = "decimal"; break;
                case "float": type = "double"; break;
                case "real": type = "float"; break;
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime": type = "DateTime"; break;
                case "datetimeoffset": type = "DateTimeOffset"; break;
                case "time": type = "TimeSpan"; break;
                case "uniqueidentifier": type = "Guid"; break;
                case "binary":
                case "varbinary":
                case "image": type = "byte[]"; break;
                default: type = "string"; break;
            }

            if (type == "string" || type == "byte[]")
            {
                return type;
            }

            return nullable ? type + "?" : type;
        }
    }
}
