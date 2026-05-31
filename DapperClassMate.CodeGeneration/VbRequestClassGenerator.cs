using System.Collections.Generic;
using System.Linq;
using System.Text;
using DapperClassMate.Core;

namespace DapperClassMate.CodeGeneration
{
    public sealed class VbRequestClassGenerator
    {
        public string Generate(
            string namespaceName,
            string className,
            IReadOnlyList<SqlParameterInfo> parameters)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Namespace {namespaceName}");
            sb.AppendLine($"    Public Class {className}");

            foreach (var parameter in parameters.Where(p => !p.IsOutput))
            {
                var propertyName = ToPascalCase(parameter.Name.TrimStart('@'));
                var vbType = ToVbType(parameter.SqlType, parameter.IsNullable);

                sb.AppendLine($"        Public Property {propertyName} As {vbType}");
            }

            sb.AppendLine("    End Class");
            sb.AppendLine("End Namespace");

            return sb.ToString();
        }

        private static string ToVbType(string sqlType, bool nullable)
        {
            string type;

            switch ((sqlType ?? string.Empty).ToLowerInvariant())
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
