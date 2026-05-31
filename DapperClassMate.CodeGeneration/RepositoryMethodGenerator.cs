using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DapperClassMate.Core;

namespace DapperClassMate.CodeGeneration
{
    public enum RepositoryReturnMode
    {
        ResultSet,
        SingleOutput,
        MultipleOutputs,
        NoReturn
    }

    public enum RepositoryCommandMode
    {
        StoredProcedure,
        Text
    }

    public sealed class RepositoryMethodGenerator
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

            var sb = new StringBuilder();

            string returnType = GetReturnType(returnMode, resultClassName, resultSetCollectionType);

            sb.AppendLine($"public {returnType} {methodName}Async(");

            if (requestClassName != null)
            {
                sb.AppendLine($"    {requestClassName} request,");
            }

            sb.AppendLine("    CancellationToken cancellationToken = default)");
            sb.AppendLine("{");
            sb.AppendLine("    using (var connection = new SqlConnection([insert connection string here])");
            sb.AppendLine("    {");
            sb.AppendLine("        var parameters = new DynamicParameters();");
            sb.AppendLine();

            foreach (var parameter in inputParams)
            {
                var cleanName = parameter.Name.TrimStart('@');
                var propertyName = ToPascalCase(cleanName);

                sb.AppendLine($"        parameters.Add(\"{parameter.Name}\", request.{propertyName});");
            }

            if (returnMode == RepositoryReturnMode.SingleOutput || returnMode == RepositoryReturnMode.MultipleOutputs)
            {
                foreach (var parameter in outputParams)
                {
                    var dbType = ToDbType(parameter.SqlType);
                    sb.AppendLine($"        parameters.Add(\"{parameter.Name}\", dbType: System.Data.DbType.{dbType}, direction: System.Data.ParameterDirection.Output);");
                }
            }

            sb.AppendLine();

            if (returnMode == RepositoryReturnMode.ResultSet)
            {
                sb.AppendLine($"        var results = await connection.QueryAsync<{resultClassName}>(");
                sb.AppendLine("            new CommandDefinition(");
                sb.AppendLine($"                \"{commandText}\",");
                sb.AppendLine("                parameters,");
                sb.AppendLine($"                commandType: {commandType},");
                sb.AppendLine($"                commandTimeout: {commandTimeout},");
                sb.AppendLine("                cancellationToken: cancellationToken));");
                sb.AppendLine();
                sb.AppendLine("        return results.AsList();");
            }
            else
            {
                sb.AppendLine("        await connection.ExecuteAsync(");
                sb.AppendLine("            new CommandDefinition(");
                sb.AppendLine($"                \"{commandText}\",");
                sb.AppendLine("                parameters,");
                sb.AppendLine($"                commandType: {commandType},");
                sb.AppendLine($"                commandTimeout: {commandTimeout},");
                sb.AppendLine("                cancellationToken: cancellationToken));");

                if (returnMode == RepositoryReturnMode.SingleOutput)
                {
                    var op = outputParams[0];
                    var csharpType = ToCSharpType(op.SqlType, op.IsNullable);

                    sb.AppendLine();
                    sb.AppendLine($"        return parameters.Get<{csharpType}>(\"{op.Name}\");");
                }
                else if (returnMode == RepositoryReturnMode.MultipleOutputs)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        return new {resultClassName}");
                    sb.AppendLine("        {");

                    foreach (var op in outputParams)
                    {
                        var cleanName = op.Name.TrimStart('@');
                        var propertyName = ToPascalCase(cleanName);
                        var csharpType = ToCSharpType(op.SqlType, op.IsNullable);

                        sb.AppendLine($"            {propertyName} = parameters.Get<{csharpType}>(\"{op.Name}\"),");
                    }

                    sb.AppendLine("        };");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

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
                    return $"async Task<{collectionType}<{resultClassName}>>";
                case RepositoryReturnMode.SingleOutput:
                    return $"async Task<{resultClassName}>";
                case RepositoryReturnMode.MultipleOutputs:
                    return $"async Task<{resultClassName}>";
                default:
                    return "async Task";
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
                return type;

            return nullable ? type + "?" : type;
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var parts = value
                .Replace("-", "_")
                .Split('_')
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Concat(parts.Select(p =>
                char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }
    }
}   
