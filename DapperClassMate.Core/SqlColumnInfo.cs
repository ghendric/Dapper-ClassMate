using System;
using System.Collections.Generic;
using System.Text;

namespace DapperClassMate.Core
{
    public sealed class SqlColumnInfo
    {
        public string Name { get; set; }
        public string SqlType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }
    }
}
