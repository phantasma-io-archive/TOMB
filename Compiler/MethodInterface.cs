namespace Phantasma.Tomb.Compiler
{
    public class MethodParameter: Node
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

        public override bool IsNodeUsed(Node node)
        {
            return true; // is this ok???
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
        public string Contract;
        public MethodImplementationType Implementation;

        public MethodInterface(LibraryDeclaration library, MethodImplementationType implementation, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, string alias = null) 
        {
            this.Name = name;
            this.Library = library;
            this.Implementation = implementation;
            this.Kind = kind;
            this.ReturnType = returnType;
            this.Parameters = parameters;

            this.Contract = this.Library.Name;

            if (alias != null)
            {
                this.Alias = alias;
            }
            else
            {
                this.Alias = $"{char.ToUpper(this.Name[0])}{this.Name.Substring(1)}";
                if (implementation == MethodImplementationType.ExtCall)
                {
                    this.Alias = this.Library.Name + '.' + this.Alias;
                }
            }            
        }

        public override string ToString()
        {
            return $"method {Name}:{ReturnType}";
        }

        public MethodInterface SetContract(string contract)
        {
            this.Contract = contract.ToLower();
            return this;
        }

        public MethodInterface SetAlias(string alias)
        {
            this.Alias = alias;
            return this;
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
