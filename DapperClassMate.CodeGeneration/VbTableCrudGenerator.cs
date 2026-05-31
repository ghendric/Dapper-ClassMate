using DapperClassMate.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DapperClassMate.CodeGeneration
{
    public sealed class VbTableCrudGenerator
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
            sb.AppendLine($"Public Async Function Insert{methodBaseName}Async(");
            sb.AppendLine($"    item As {modelClassName},");
            sb.AppendLine("    Optional cancellationToken As CancellationToken = Nothing) As Task(Of Integer)");
            sb.AppendLine("    Using connection As New SqlConnection([insert connection string here])");
            sb.AppendLine("        Dim sql = \"");
            sb.AppendLine("INSERT INTO " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("(" + string.Join(", ", insertColumns.Select(c => QuoteSqlIdentifier(c.Name))) + ")");
            sb.AppendLine("VALUES");
            sb.AppendLine("(" + string.Join(", ", insertColumns.Select(c => "@" + ToPascalCase(c.Name))) + ");\"");
            sb.AppendLine();
            sb.AppendLine("        Return Await connection.ExecuteAsync(");
            sb.AppendLine("            New CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                item,");
            sb.AppendLine("                commandType:=CommandType.Text,");
            sb.AppendLine($"                commandTimeout:={commandTimeout},");
            sb.AppendLine("                cancellationToken:=cancellationToken))");
            sb.AppendLine("    End Using");
            sb.AppendLine("End Function");

            return sb.ToString();
        }

        private static string GenerateReadMethod(
            string objectName,
            string modelClassName,
            string methodBaseName,
            int commandTimeout,
            string resultSetCollectionType)
        {
            var collectionType = NormalizeResultSetCollectionType(resultSetCollectionType);
            var sb = new StringBuilder();
            sb.AppendLine($"Public Async Function Get{methodBaseName}Async(");
            sb.AppendLine($"    Optional cancellationToken As CancellationToken = Nothing) As Task(Of {collectionType}(Of {modelClassName}))");
            sb.AppendLine("    Using connection As New SqlConnection([insert connection string here])");
            sb.AppendLine($"        Dim results = Await connection.QueryAsync(Of {modelClassName})(");
            sb.AppendLine("            New CommandDefinition(");
            sb.AppendLine($"                \"SELECT * FROM {QuoteSchemaQualifiedName(objectName)}\",");
            sb.AppendLine("                commandType:=CommandType.Text,");
            sb.AppendLine($"                commandTimeout:={commandTimeout},");
            sb.AppendLine("                cancellationToken:=cancellationToken))");
            sb.AppendLine();
            sb.AppendLine("        Return results.AsList()");
            sb.AppendLine("    End Using");
            sb.AppendLine("End Function");

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
            sb.AppendLine($"Public Async Function Update{methodBaseName}Async(");
            sb.AppendLine($"    item As {modelClassName},");
            sb.AppendLine("    Optional cancellationToken As CancellationToken = Nothing) As Task(Of Integer)");
            sb.AppendLine("    Using connection As New SqlConnection([insert connection string here])");
            sb.AppendLine("        Dim sql = \"");
            sb.AppendLine("UPDATE " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("SET");
            sb.AppendLine(string.Join("," + Environment.NewLine, updateColumns.Select(c =>
                "    " + QuoteSqlIdentifier(c.Name) + " = @" + ToPascalCase(c.Name))));
            sb.AppendLine("WHERE");
            sb.AppendLine(string.Join(Environment.NewLine + "AND ", keyColumns.Select(c =>
                QuoteSqlIdentifier(c.Name) + " = @" + ToPascalCase(c.Name))) + ";\"");
            sb.AppendLine();
            sb.AppendLine("        Return Await connection.ExecuteAsync(");
            sb.AppendLine("            New CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                item,");
            sb.AppendLine("                commandType:=CommandType.Text,");
            sb.AppendLine($"                commandTimeout:={commandTimeout},");
            sb.AppendLine("                cancellationToken:=cancellationToken))");
            sb.AppendLine("    End Using");
            sb.AppendLine("End Function");

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
            sb.AppendLine($"Public Async Function Delete{methodBaseName}Async(");

            foreach (var keyColumn in keyColumns)
            {
                sb.AppendLine($"    {ToCamelCase(keyColumn.Name)} As {ToVbType(keyColumn.SqlType, keyColumn.IsNullable)},");
            }

            sb.AppendLine("    Optional cancellationToken As CancellationToken = Nothing) As Task(Of Integer)");
            sb.AppendLine("    Using connection As New SqlConnection([insert connection string here])");
            sb.AppendLine("        Dim sql = \"");
            sb.AppendLine("DELETE FROM " + QuoteSchemaQualifiedName(objectName));
            sb.AppendLine("WHERE");
            sb.AppendLine(string.Join(Environment.NewLine + "AND ", keyColumns.Select(c =>
                QuoteSqlIdentifier(c.Name) + " = @" + ToCamelCase(c.Name))) + ";\"");
            sb.AppendLine();
            sb.AppendLine("        Return Await connection.ExecuteAsync(");
            sb.AppendLine("            New CommandDefinition(");
            sb.AppendLine("                sql,");
            sb.AppendLine("                New With {");

            for (var i = 0; i < keyColumns.Count; i++)
            {
                var keyColumn = keyColumns[i];
                var comma = i == keyColumns.Count - 1 ? string.Empty : ",";
                var parameterName = ToCamelCase(keyColumn.Name);

                sb.AppendLine($"                    .{parameterName} = {parameterName}{comma}");
            }

            sb.AppendLine("                },");
            sb.AppendLine("                commandType:=CommandType.Text,");
            sb.AppendLine($"                commandTimeout:={commandTimeout},");
            sb.AppendLine("                cancellationToken:=cancellationToken))");
            sb.AppendLine("    End Using");
            sb.AppendLine("End Function");

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

        private static string NormalizeResultSetCollectionType(string collectionType)
        {
            var clean = (collectionType ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(clean))
            {
                return "IReadOnlyList";
            }

            var genericMarkerIndex = clean.IndexOf('<');
            if (genericMarkerIndex > 0)
            {
                clean = clean.Substring(0, genericMarkerIndex).Trim();
            }

            switch (clean.ToLowerInvariant())
            {
                case "list":
                    return "List";
                case "ilist":
                    return "IList";
                case "icollection":
                    return "ICollection";
                case "ienumerable":
                    return "IEnumerable";
                case "ireadonlycollection":
                    return "IReadOnlyCollection";
                case "ireadonlylist":
                    return "IReadOnlyList";
                default:
                    return "IReadOnlyList";
            }
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

        private static string ToVbType(string sqlType, bool nullable)
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
                case "int": type = "Integer"; break;
                case "bigint": type = "Long"; break;
                case "smallint": type = "Short"; break;
                case "tinyint": type = "Byte"; break;
                case "bit": type = "Boolean"; break;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": type = "Decimal"; break;
                case "float": type = "Double"; break;
                case "real": type = "Single"; break;
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime": type = "DateTime"; break;
                case "datetimeoffset": type = "DateTimeOffset"; break;
                case "time": type = "TimeSpan"; break;
                case "uniqueidentifier": type = "Guid"; break;
                case "binary":
                case "varbinary":
                case "image": type = "Byte()"; break;
                default: type = "String"; break;
            }

            if (type == "String" || type == "Byte()")
            {
                return type;
            }

            return nullable ? type + "?" : type;
        }
    }
}
