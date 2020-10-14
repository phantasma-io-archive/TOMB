namespace Phantasma.Tomb.Compiler
{
    public class MethodParameter
    {
        public string Name;
        public VarKind Kind;

        public readonly bool Patchable;

        public MethodParameter(string name, VarKind kind)
        {
            Name = name;
            Kind = kind;
            this.Patchable = kind == VarKind.Unknown;
        }

        public override string ToString()
        {
            return $"{Name}:{Kind}";
        }
    }

    public enum MethodImplementationType
    {
        ExtCall,
        Contract,
        Custom
    }

    public class MethodInterface
    {
        public string Name;
        public LibraryDeclaration Library;
        public MethodKind Kind;
        public VarKind ReturnType;
        public MethodParameter[] Parameters;
        public string Alias;
        public MethodImplementationType Implementation;

        public MethodInterface(LibraryDeclaration library, MethodImplementationType implementation, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, string alias = null) 
        {
            this.Name = name;
            this.Library = library;
            this.Implementation = implementation;
            this.Kind = kind;
            this.ReturnType = returnType;
            this.Parameters = parameters;

            this.Alias = alias != null? alias : $"{this.Library.Name}.{char.ToUpper(this.Name[0])}{this.Name.Substring(1)}";
        }

        public override string ToString()
        {
            return $"method {Name}:{ReturnType}";
        }

        public void PatchParam(string name, VarKind kind)
        {
            foreach (var arg in Parameters)
            {
                if (arg.Name == name && arg.Patchable)
                {
                    arg.Kind = kind;
                    break;
                }
            }
        }
    }
}
