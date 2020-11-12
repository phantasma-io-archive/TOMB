using System.Collections.Generic;

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
        Enum,
        Task,
        Any,
        Method,
        Struct,
        Decimal,
        Storage_Map,
        Storage_List,
        Storage_Set,
    }

    public abstract class VarType
    {
        public readonly VarKind Kind;

        public abstract string Key { get; }

        /*public readonly static VarType None = Find(VarKind.None);
        public readonly static VarType Address = Find(VarKind.Address);
        public readonly static VarType Bool = Find(VarKind.Bool);*/

        protected VarType(VarKind kind)
        {
            Kind = kind;

            var key = this.Key;
            if (_cache.ContainsKey(key))
            {
                throw new CompilerException("internal error initializing type: " + key);
            }
        }

        private static Dictionary<string, VarType> _cache = new Dictionary<string, VarType>();

        public bool IsStorageBound => Kind == VarKind.Storage_Map || Kind == VarKind.Storage_List || Kind == VarKind.Storage_Set;

        public static VarType Find(VarKind kind, object extra = null)
        {
            var key = kind.ToString();
            if (extra!= null)
            {
                key = $"{key}<{extra}>";
            }

            if (_cache.ContainsKey(key))
            {
                return _cache[key];
            }

            VarType result;

            switch (kind)
            {
                case VarKind.Number:
                case VarKind.Bool:
                case VarKind.String:
                case VarKind.Timestamp:
                case VarKind.Address:
                case VarKind.Hash:
                case VarKind.Bytes:
                case VarKind.Task:
                case VarKind.None:
                case VarKind.Generic:
                case VarKind.Any:
                case VarKind.Storage_List:
                case VarKind.Storage_Map:
                case VarKind.Storage_Set:
                case VarKind.Unknown:
                    result =  new PrimitiveVarType(kind);
                    break;

                case VarKind.Decimal:
                    result = new DecimalVarType((int) extra);
                    break;

                case VarKind.Struct:
                    result = new StructVarType((string)extra);
                    break;

                case VarKind.Enum:
                    result = new EnumVarType((string)extra);
                    break;

                case VarKind.Method:
                    result = new MethodVarType((string)extra);
                    break;

                default:
                    throw new CompilerException($"Could not initialize type: {kind}");
            }

            _cache[key] = result;
            return result;
        }
    }

    public class PrimitiveVarType : VarType
    {
        public override string Key => $"{Kind}";

        public PrimitiveVarType(VarKind kind) : base(kind)
        {

        }

        public override string ToString()
        {
            return Kind.ToString();
        }
    }

    public class StructVarType : VarType
    {
        public override string Key => $"{Kind}<{name}>";

        public readonly string name;

        public StructDeclaration decl;

        public StructVarType(string name) : base(VarKind.Struct)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return $"{Kind}<{name}>";
        }
    }

    public class EnumVarType : VarType
    {
        public override string Key => $"{Kind}<{name}>";

        public readonly string name;

        public EnumDeclaration decl;

        public EnumVarType(string name) : base(VarKind.Enum)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return $"{Kind}<{name}>";
        }
    }

    public class DecimalVarType : VarType
    {
        public readonly int decimals;

        public override string Key => $"{Kind}<{decimals}>";
        public DecimalVarType(int decimals) : base(VarKind.Decimal)
        {
            this.decimals = decimals;
        }

        public override string ToString()
        {
            return $"{Kind}<{decimals}>";
        }
    }


    public class MethodVarType : VarType
    {
        public readonly string name;

        public override string Key => $"{Kind}<{name}>";

        public MethodVarType(string name) : base(VarKind.Method)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return $"{Kind}<{name}>";
        }
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
        Power,
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
        Property,
    }
}
