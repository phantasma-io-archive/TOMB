namespace Phantasma.Tomb.Compiler
{
    public enum VarKind
    {
        Unknown,
        None,
        Number,
        Bool,
        String,
        Timestamp,
        Address,
        Bytes,
        Method,
        Map
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
    }

    public enum MethodKind
    {
        Method,
        Constructor,
        Task,
        Trigger,
    }
}
