namespace Phantasma.Tomb.Compiler
{
    public class MethodParameter
    {
        public string Name;
        public VarKind Kind;

        public MethodParameter(string name, VarKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"{Name}:{Kind}";
        }
    }

    public class MethodInterface
    {
        public string Name;
        public LibraryDeclaration Library;
        public MethodKind Kind;
        public VarKind ReturnType;
        public MethodParameter[] Parameters;

        public MethodInterface(LibraryDeclaration library, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters) 
        {
            this.Name = name;
            this.Library = library;
            this.Kind = kind;
            this.ReturnType = returnType;
            this.Parameters = parameters;
        }

        public override string ToString()
        {
            return $"method {Name}:{ReturnType}";
        }
    }
}
