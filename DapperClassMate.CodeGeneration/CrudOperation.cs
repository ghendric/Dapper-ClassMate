using System;

namespace DapperClassMate.CodeGeneration
{
    [Flags]
    public enum CrudOperation
    {
        None = 0,
        Create = 1,
        Read = 2,
        Update = 4,
        Delete = 8
    }
}
