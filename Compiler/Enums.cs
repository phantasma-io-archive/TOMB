namespace Phantasma.Tomb.Compiler
{
    public enum VarKind
    {
        None,
        Unknown,
        Generic,
        Number,
        Bool,
        String,
        Timestamp,
        Address,
        Hash,
        Bytes,
        Any,
        Method,
        Storage_Map,
        Storage_List,
        Storage_Set,
    }

    public enum VarStorage
    {
        Global,
        Local,
        Argument,
    }

    public enum OperatorKind
    {
        Unknown,
        Assignment,
        Equal,
        Different,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Modulus,
        ShiftRight,
        ShiftLeft,
        Or,
        And,
        Xor,
    }

    public enum MethodKind
    {
        Method,
        Constructor,
        Task,
        Trigger,
    }
}
