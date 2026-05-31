using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DapperClassMate.Core;

namespace DapperClassMate.CodeGeneration
{
    public sealed class RequestClassGenerator
    {
        public string Generate(
            string namespaceName,
            string className,
            IReadOnlyList<SqlParameterInfo> parameters)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {className}");
            sb.AppendLine("    {");

            foreach (var parameter in parameters.Where(p => !p.IsOutput))
            {
                var propertyName = ToPascalCase(parameter.Name.TrimStart('@'));
                var csharpType = ToCSharpType(parameter.SqlType, parameter.IsNullable);

                sb.AppendLine($"        public {csharpType} {propertyName} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string ToCSharpType(string sqlType, bool nullable)
        {
            string type;

            switch (sqlType.ToLowerInvariant())
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