using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Tomb.Compiler
{
    public class NFT : Contract
    {
        public VarType romType;
        public VarType ramType;

        public StructVarType nftType;

        public NFT(string name, VarType romType, VarType ramType) : base(name, ModuleKind.NFT)
        {
            this.romType = romType;
            this.ramType = ramType;

            this.nftType = (StructVarType)VarType.Find(VarKind.Struct, name);
            //this.nftType.decl = new StructDeclaration(name, new[] { new StructField("id", VarKind.Number), new StructField("owner", VarKind.Address), new StructField("chain", VarKind.String), new StructField("rom", romType), new StructField("ram", ramType) });

            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_id", VarType.Find(VarKind.Number), VarStorage.Global));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_rom", romType, VarStorage.Global));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_ram", ramType, VarStorage.Global));
        }
    }
}
