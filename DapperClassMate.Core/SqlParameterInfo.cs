using System;

namespace DapperClassMate.Core
{
    public sealed class SqlParameterInfo
    {
        public string Name { get; set; }    
        public string SqlType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsOutput { get; set; }
        public int MaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
    }
}
