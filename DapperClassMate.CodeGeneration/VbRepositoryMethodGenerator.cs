using System.Collections.Generic;
using System.Linq;
using System.Text;
using DapperClassMate.Core;

namespace DapperClassMate.CodeGeneration
{
    public sealed class VbRepositoryMethodGenerator
    {
        public string Generate(
            string commandText,
            string requestClassName,
            string resultClassName,
            string methodName,
            IReadOnlyList<SqlParameterInfo> parameters,
            RepositoryReturnMode returnMode,
            RepositoryCommandMode commandMode = RepositoryCommandMode.StoredProcedure,
            int commandTimeout = 30,
            string resultSetCollectionType = "IReadOnlyList")
        {
            var inputParams = parameters.Where(p => !p.IsOutput).ToList();
            var outputParams = parameters.Where(p => p.IsOutput).ToList();
            var commandType = GetCommandType(commandMode);
            var returnType = GetReturnType(returnMode, resultClassName, resultSetCollectionType);

            var sb = new StringBuilder();

            sb.AppendLine($"Public Async Function {methodName}Async(");

            if (requestClassName != null)
            {
                sb.AppendLine($"    request As {requestClassName},");
            }

            sb.AppendLine($"    Optional cancellationToken As CancellationToken = Nothing) As {returnType}");
            sb.AppendLine("    Using connection As New SqlConnection([insert connection string here])");
            sb.AppendLine("        Dim parameters = New DynamicParameters()");
            sb.AppendLine();

            foreach (var parameter in inputParams)
            {
                var cleanName = parameter.Name.TrimStart('@');
                var propertyName = ToPascalCase(cleanName);

                sb.AppendLine($"        parameters.Add(\"{parameter.Name}\", request.{propertyName})");
            }

            if (returnMode == RepositoryReturnMode.SingleOutput || returnMode == RepositoryReturnMode.MultipleOutputs)
            {
                foreach (var parameter in outputParams)
                {
                    var dbType = ToDbType(parameter.SqlType);
                    sb.AppendLine($"        parameters.Add(\"{parameter.Name}\", dbType:=System.Data.DbType.{dbType}, direction:=System.Data.ParameterDirection.Output)");
                }
            }

            sb.AppendLine();

            if (returnMode == RepositoryReturnMode.ResultSet)
            {
                sb.AppendLine($"        Dim results = Await connection.QueryAsync(Of {resultClassName})(");
                sb.AppendLine("            New CommandDefinition(");
                sb.AppendLine($"                \"{commandText}\",");
                sb.AppendLine("                parameters,");
                sb.AppendLine($"                commandType:={commandType},");
                sb.AppendLine($"                commandTimeout:={commandTimeout},");
                sb.AppendLine("                cancellationToken:=cancellationToken))");
                sb.AppendLine();
                sb.AppendLine("        Return results.AsList()");
            }
            else
            {
                sb.AppendLine("        Await connection.ExecuteAsync(");
                sb.AppendLine("            New CommandDefinition(");
                sb.AppendLine($"                \"{commandText}\",");
                sb.AppendLine("                parameters,");
                sb.AppendLine($"                commandType:={commandType},");
                sb.AppendLine($"                commandTimeout:={commandTimeout},");
                sb.AppendLine("                cancellationToken:=cancellationToken))");

                if (returnMode == RepositoryReturnMode.SingleOutput)
                {
                    var op = outputParams[0];
                    var vbType = ToVbType(op.SqlType, op.IsNullable);

                    sb.AppendLine();
                    sb.AppendLine($"        Return parameters.Get(Of {vbType})(\"{op.Name}\")");
                }
                else if (returnMode == RepositoryReturnMode.MultipleOutputs)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        Return New {resultClassName} With {{");

                    for (var i = 0; i < outputParams.Count; i++)
                    {
                        var op = outputParams[i];
                        var cleanName = op.Name.TrimStart('@');
                        var propertyName = ToPascalCase(cleanName);
                        var vbType = ToVbType(op.SqlType, op.IsNullable);
                        var suffix = i == outputParams.Count - 1 ? string.Empty : ",";

                        sb.AppendLine($"            .{propertyName} = parameters.Get(Of {vbType})(\"{op.Name}\"){suffix}");
                    }

                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("    End Using");
            sb.AppendLine("End Function");

            return sb.ToString();
        }

        private static string GetCommandType(RepositoryCommandMode commandMode)
        {
            switch (commandMode)
            {
                case RepositoryCommandMode.Text:
                    return "CommandType.Text";
                default:
                    return "CommandType.StoredProcedure";
            }
        }

        private static string GetReturnType(RepositoryReturnMode returnMode, string resultClassName, string resultSetCollectionType)
        {
            switch (returnMode)
            {
                case RepositoryReturnMode.ResultSet:
                    var collectionType = NormalizeResultSetCollectionType(resultSetCollectionType);
                    return $"Task(Of {collectionType}(Of {resultClassName}))";
                case RepositoryReturnMode.SingleOutput:
                case RepositoryReturnMode.MultipleOutputs:
                    return $"Task(Of {resultClassName})";
                default:
                    return "Task";
            }
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

        private static string ToDbType(string sqlType)
        {
            var clean = (sqlType ?? string.Empty).ToLowerInvariant();
            var paren = clean.IndexOf('(');
            if (paren >= 0)
            {
                clean = clean.Substring(0, paren);
            }

            switch (clean)
            {
                case "int": return "Int32";
                case "bigint": return "Int64";
                case "smallint": return "Int16";
                case "tinyint": return "Byte";
                case "bit": return "Boolean";
                case "decimal":
                case "numeric": return "Decimal";
                case "money":
                case "smallmoney": return "Currency";
                case "float": return "Double";
                case "real": return "Single";
                case "date": return "Date";
                case "datetime":
                case "datetime2":
                case "smalldatetime": return "DateTime";
                case "datetimeoffset": return "DateTimeOffset";
                case "time": return "Time";
                case "uniqueidentifier": return "Guid";
                case "binary":
                case "varbinary":
                case "image": return "Binary";
                case "char":
                case "nchar": return "StringFixedLength";
                default: return "String";
            }
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

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var parts = value
                .Replace("-", "_")
                .Split('_')
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Concat(parts.Select(p =>
                char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }
    }
}
