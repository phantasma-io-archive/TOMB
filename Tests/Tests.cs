using NUnit.Framework;
using NUnit.Framework.Internal;
using Phantasma.API;
using Phantasma.Storage;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Log;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.Tomb.Compiler;
using Phantasma.VM;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        public class TestVM : VirtualMachine
        {
            private Dictionary<string, Func<VirtualMachine, ExecutionState>> _interops = new Dictionary<string, Func<VirtualMachine, ExecutionState>>();
            private Func<string, ExecutionContext> _contextLoader;
            private Dictionary<string, ScriptContext> contexts;
            private Dictionary<byte[], byte[]> storage;

            public TestVM(Module module, Dictionary<byte[], byte[]> storage, ContractMethod method) : base(module.script, (uint)method.offset, module.Name)
            {
                this.storage = storage;
                RegisterContextLoader(ContextLoader);

                RegisterMethod("ABI()", ExtCalls.Constructor_ABI);
                RegisterMethod("Address()", ExtCalls.Constructor_Address);
                RegisterMethod("Hash()", ExtCalls.Constructor_Hash);
                RegisterMethod("Timestamp()", ExtCalls.Constructor_Timestamp);

                RegisterMethod("Data.Set", Data_Set);
                RegisterMethod("Data.Get", Data_Get);
                RegisterMethod("Data.Delete", Data_Delete);
                contexts = new Dictionary<string, ScriptContext>();
            }

            private ExecutionContext ContextLoader(string contextName)
            {
                if (contexts.ContainsKey(contextName))
                    return contexts[contextName];

                return null;
            }

            public byte[] BuildScript(string[] lines)
            {
                IEnumerable<Semanteme> semantemes = null;
                try
                {
                    semantemes = Semanteme.ProcessLines(lines);
                }
                catch (Exception e)
                {
                    throw new Exception("Error parsing the script");
                }

                var sb = new ScriptBuilder();
                byte[] script = null;

                try
                {
                    script = sb.ToScript();
                }
                catch (Exception e)
                {
                    throw new Exception("Error assembling the script");
                }

                return script;
            }

            public void RegisterMethod(string method, Func<VirtualMachine, ExecutionState> callback)
            {
                _interops[method] = callback;
            }

            public void RegisterContextLoader(Func<string, ExecutionContext> callback)
            {
                _contextLoader = callback;
            }

            public override ExecutionState ExecuteInterop(string method)
            {
                if (_interops.ContainsKey(method))
                {
                    return _interops[method](this);
                }

                throw new VMException(this, $"unknown interop: {method}");
            }

            public override ExecutionContext LoadContext(string contextName)
            {
                if (_contextLoader != null)
                {
                    return _contextLoader(contextName);
                }

                throw new VMException(this, $"unknown context: {contextName}");
            }

            public override void DumpData(List<string> lines)
            {
                // do nothing
            }

            private ExecutionState Data_Get(VirtualMachine vm)
            {
                var contractName = vm.PopString("contract");
                //vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

                var field = vm.PopString("field");
                var key = SmartContract.GetKeyForField(contractName, field, false);

                var type_obj = vm.Stack.Pop();
                var vmType = type_obj.AsEnum<VMType>();

                if (vmType == VMType.Object)
                {
                    vmType = VMType.Bytes;
                }

                var value_bytes = this.storage.ContainsKey(key) ? this.storage[key] : new byte[0];
                var val = new VMObject();
                val.SetValue(value_bytes, vmType);

                val.SetValue(value_bytes, vmType);
                this.Stack.Push(val);

                return ExecutionState.Running;
            }

            private ExecutionState Data_Set(VirtualMachine vm)
            {
                // for security reasons we don't accept the caller to specify a contract name
                var contractName = vm.CurrentContext.Name;

                var field = vm.PopString("field");
                var key = SmartContract.GetKeyForField(contractName, field, false);

                var obj = vm.Stack.Pop();
                var valBytes = obj.AsByteArray();

                this.storage[key] = valBytes;

                return ExecutionState.Running;
            }

            private ExecutionState Data_Delete(VirtualMachine vm)
            {
                // for security reasons we don't accept the caller to specify a contract name
                var contractName = vm.CurrentContext.Name;

                var field = vm.PopString("field");
                var key = SmartContract.GetKeyForField(contractName, field, false);

                this.storage.Remove(key);

                return ExecutionState.Running;
            }
        }

        [Test]
        public void Switch()
        {
            var sourceCode =
                @"
contract test {
    public check(x:number): string {
        switch (x) {
            case 0: return ""zero"";
            case 1: return ""one"";
            case 2: return ""two"";
            default: return ""other"";
        }                  
     }}";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
            
            var check = contract.abi.FindMethod("check");
            Assert.IsNotNull(check);
            
            // test different cases
            for (int i=-1; i<=4; i++)
            {
                var vm = new TestVM(contract, storage, check);
                vm.Stack.Push(VMObject.FromObject(i));
                var state = vm.Execute();
                Assert.IsTrue(state == ExecutionState.Halt);
                var result = vm.Stack.Pop().AsString();

                string expected;
                switch (i)
                {
                    case 0: expected = "zero"; break;
                    case 1: expected = "one"; break;
                    case 2: expected = "two"; break;
                    default: expected = "other"; break;
                }

                Assert.IsTrue(result == expected);
            }
        }


        [Test]
        public void TestCounter()
        {
            var sourceCode =
                @"
contract test {
    global counter: number;
    
    constructor(owner:address)	{
        counter:= 0; 
    }
    
    public increment() {
        if (counter < 0) {
            throw ""invalid state"";
        }   
                
        counter += 1;
     }}";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            // call constructor
            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call increment
            var increment = contract.abi.FindMethod("increment");
            Assert.IsNotNull(increment);

            vm = new TestVM(contract, storage, increment);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);
        }


        [Test]
        public void MinMax()
        {
            var sourceCode =
                @"contract test{
                    import Math;
                    public calculate(a:number, b:number):number {
                        return Math.min(a, b);
                    }}";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            // call increment
            var calculate = contract.abi.FindMethod("calculate");
            Assert.IsNotNull(calculate);

            vm = new TestVM(contract, storage, calculate);
            vm.Stack.Push(VMObject.FromObject(5));
            vm.Stack.Push(VMObject.FromObject(2));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == 2);            
        }


        [Test]
        public void TypeOf()
        {
            var sourceCode = new string[] {
                "contract test{" ,
                "public returnType() : type	{" ,
                "return $TYPE_OF(string);" ,
                "}}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var keys = PhantasmaKeys.Generate();

            // call returnType
            var returnType = contract.abi.FindMethod("returnType");
            Assert.IsNotNull(returnType);

            vm = new TestVM(contract, storage, returnType);
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 0);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var vmType = obj.AsEnum<VMType>();

            Assert.IsTrue(vmType == VMType.String);
        }

        [Test]
        public void StringsSimple()
        {
            var str = "hello";

            var sourceCode =
                "contract test{\n" +
                "global name: string;\n" +
                "constructor(owner:address)	{\n" +
                "name:= \"" + str + "\";\n}" +
                "public getLength():number {\n" +
                "return name.length();\n" +
                "}}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getLength
            var getLength = contract.abi.FindMethod("getLength");
            Assert.IsNotNull(getLength);

            vm = new TestVM(contract, storage, getLength);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var len = obj.AsNumber();

            var expectedLength = str.Length;

            Assert.IsTrue(len == expectedLength);
        }

        [Test]
        public void DecimalsSimple()
        {
            var valStr = "2.4587";
            var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);
            var decimals = 8;

            var sourceCode =
                "contract test{\n" +
                $"global amount: decimal<{decimals}>;\n" +
                "constructor(owner:address)	{\n" +
                "amount := "+valStr+";\n}" +
                "public getValue():number {\n" +
                "return amount;\n}" +
                "public getLength():number {\n" +
                "return amount.decimals();\n}" +
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getVal
            var getValue = contract.abi.FindMethod("getValue");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsNumber();
            var expectedVal = UnitConversion.ToBigInteger(val, decimals);

            Assert.IsTrue(newVal == expectedVal);

            // call getLength
            var getLength = contract.abi.FindMethod("getLength");
            Assert.IsNotNull(getLength);

            vm = new TestVM(contract, storage, getLength);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            Assert.IsTrue(vm.Stack.Count == 1);

            obj = vm.Stack.Pop();
            var len = obj.AsNumber();

            Assert.IsTrue(len == decimals);
        }

        [Test]
        public void DecimalsPrecision()
        {
            var valStr = "2.4587";
            var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);

            var sourceCode =
                "contract test{\n" +
                $"global amount: decimal<3>;\n" +
                "constructor(owner:address)	{\n" +
                "amount := " + valStr + ";\n}" +
                "}\n";

            var parser = new Compiler();

            try
            {
                var contract = parser.Process(sourceCode).First();
                Assert.Fail("should have throw compile error");
            }
            catch (CompilerException e)
            {
                Assert.IsTrue(e.Message.ToLower().Contains("precision"));
            }

        }

        public enum MyEnum
        {
            A,
            B,
            C,
        }

        [Test]
        public void Enums()
        {
            string[] sourceCode = new string[] {
                "enum MyEnum { A, B, C}",
                "contract test{",
                $"global state: MyEnum;",
                "constructor(owner:address)	{" ,
                "state := MyEnum.B;}" ,
                "public getValue():MyEnum {" ,
                "return state;}" ,
                "public isSet(val:MyEnum):bool {" ,
                "return state.isSet(val);}" ,
                "}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getVal
            var getValue = contract.abi.FindMethod("getValue");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsEnum<MyEnum>();
            var expectedVal = MyEnum.B;

            Assert.IsTrue(newVal == expectedVal);
        }

        [Test]
        public void Properties()
        {
            string[] sourceCode = new string[] {
                "token TEST  {",
                    "property name:string = \"Unit test\";",
                    "   global _feesSymbol:string;",
                    $"  property feesSymbol:string = _feesSymbol;",
                    "   constructor(owner:address)	{" ,
                    "       _feesSymbol := \"KCAL\";" ,
                    "}}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getFeesSymbol
            var getValue = contract.abi.FindMethod("getFeesSymbol");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsString();
            var expectedVal = "KCAL";

            Assert.IsTrue(newVal == expectedVal);
        }

        [Test]
        public void Bools()
        {
            string[] sourceCode = new string[] {
                "token TEST {",
                    "global _contractPaused:bool;",
                    "property name: string = \"Ghost\";	",
                    "   constructor(owner:address)	{" ,
                    "       _contractPaused:= false;" ,
                    "}}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);
        }

        [Test]
        public void UpdateStringMethod()
        {
            string[] sourceCode = new string[] {
                "token TEST  {",
                    "property name:string = \"Unit test\";",
                    "   global _feesSymbol:string;",
                    $"  property feesSymbol:string = _feesSymbol;",
                    "   constructor(owner:address)	{" ,
                    "       _feesSymbol := \"KCAL\";" ,
                    "}",
                    "public updateFeesSymbol(feesSymbol:string) {",
                    "   _feesSymbol:= feesSymbol;",
                    "}",
                    "}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call updateFeesSymbol
            var updateValue = contract.abi.FindMethod("updateFeesSymbol");
            Assert.IsNotNull(updateValue);

            vm = new TestVM(contract, storage, updateValue);
            vm.Stack.Push(VMObject.FromObject("SOUL"));
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getFeesSymbol
            var getValue = contract.abi.FindMethod("getFeesSymbol");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsString();
            var expectedVal = "SOUL";

            Assert.IsTrue(newVal == expectedVal);
        }

        [Test]
        public void UpdateNumberMethod()
        {
            string[] sourceCode = new string[] {
                "token GHOST {",
                    "	global _infuseMultiplier:number;",
                    "	property name:string = \"test\";",
                    "	property infuseMultiplier:number = _infuseMultiplier;",
                    "	constructor (owner:address) { _infuseMultiplier := 1;	}",
                    "	public updateInfuseMultiplier(infuseMultiplier:number) 	{	_infuseMultiplier := infuseMultiplier;	}",
                    "}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call updateInfuseMultiplier
            var updateValue = contract.abi.FindMethod("updateInfuseMultiplier");
            Assert.IsNotNull(updateValue);

            vm = new TestVM(contract, storage, updateValue);
            vm.Stack.Push(VMObject.FromObject("4"));
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getInfuseMultiplier
            var getValue = contract.abi.FindMethod("getInfuseMultiplier");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsNumber();
            var expectedVal = 4;

            Assert.IsTrue(newVal == expectedVal);
        }

        [Test]
        public void QueryMethodAddress()
        {
            string[] sourceCode = new string[] {
                "token TEST  {",
                    "property name:string = \"Unit test\";",
                    "   global _feesAddress:address;",
                    $"  property feesAddress:address = _feesAddress;",
                    "   constructor(owner:address)	{" ,
                    "       _feesAddress := @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;" ,
                    "}}"
            };

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            vm = new TestVM(contract, storage, constructor);
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);

            // call getFeesAddress
            var getValue = contract.abi.FindMethod("getFeesAddress");
            Assert.IsNotNull(getValue);

            vm = new TestVM(contract, storage, getValue);
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.AsString();
            var expectedVal = "P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM";

            Assert.IsTrue(newVal == expectedVal);
        }

        [Test]
        public void IsWitness()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "global _address:address;" +
                "global _owner:address;" +
                "constructor(owner:address)	{\n" +
                "_address := @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;\n" +
                "_owner:= owner;\n" +
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                "Runtime.expect(Runtime.isWitness(_address), \"witness failed\");\n" +
                "}\n"+
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "doStuff", keys.Address).
                    SpendGas(keys.Address).
                    EndScript());

            var ex = Assert.Throws<ChainException>(() => simulator.EndBlock());
            Assert.That(ex.Message, Is.EqualTo("add block @ main failed, reason: witness failed"));
        }

        [Test]
        public void NFTs()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            string symbol = "ATEST";
            string name = "Test";

            var sourceCode =
                @"struct someStruct
                {
                    created:timestamp;
                    creator:address;
                    royalties:number;
                    name:string;
                    description:string;
                    imageURL:string;
                    infoURL:string;
                }
                token " + symbol + @" {
                    import Runtime;
                    import Time;
                    import NFT;
                    import Map;
                    global _address:address;
                    global _owner:address;
                    global _unlockStorageMap: storage_map<number, number>;

                    property name:string = """+ name + @""";

                    nft myNFT<someStruct, number> {

                        import Call;
                        import Map;

                        property name:string {
                            return _ROM.name;
                        }

                        property description:string {
                            return _ROM.description;
                        }

                        property imageURL:string {
                            return _ROM.imageURL;
                        }

                        property infoURL:string {
                            return _ROM.infoURL;
                        }

                        property unlockCount:number {
                            local count:number := Call.interop<number>(""Map.Get"",  ""ATEST"", ""_unlockStorageMap"", _tokenID, $TYPE_OF(number));
                            return count;
                        }
                    }

                    import Call;
                    constructor(owner:address)	{
                        _address := @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;
                        _owner:= owner;
                        NFT.createSeries(owner, $THIS_SYMBOL, 0, 999, TokenSeries.Unique, myNFT);
                    }

                    public mint(dest:address):number {
                        local rom:someStruct := Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");
                        local tokenID:number := NFT.mint(_address, dest, $THIS_SYMBOL, rom, 0, 0);
                        _unlockStorageMap.set(tokenID, 0);
                        Call.interop<none>(""Map.Set"",  ""_unlockStorageMap"", tokenID, 111);
                        return tokenID;
                    }

                    public readName(nftID:number): string {
                        local romInfo:someStruct := NFT.readROM<someStruct>($THIS_SYMBOL, nftID);
                        return romInfo.name;
                    }

                    public readOwner(nftID:number): address {
                        local nftInfo:NFT := NFT.read($THIS_SYMBOL, nftID);
                        return nftInfo.owner;
                    }
                }";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();
            //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract..asm);
            //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract.SubModules.First().asm);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Nexus.CreateToken", keys.Address, symbol, name, 0, 0, TokenFlags.Burnable | TokenFlags.Transferable, contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            var otherKeys = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "mint", otherKeys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            var block = simulator.EndBlock().First();

            var result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            var obj = VMObject.FromBytes(result);
            var nftID = obj.AsNumber();
            Assert.IsTrue(nftID > 0);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "readName",nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            obj = VMObject.FromBytes(result);
            var nftName = obj.AsString();
            Assert.IsTrue(nftName == "hello");

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "readOwner", nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            obj = VMObject.FromBytes(result);
            var nftOwner = obj.AsAddress();
            Assert.IsTrue(nftOwner == otherKeys.Address);

            var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes(symbol), 0, new DummyLogger());
            mempool?.SetKeys(keys);

            var api = new NexusAPI(simulator.Nexus);

            var nft = (TokenDataResult)api.GetNFT(symbol, nftID.ToString(), true);
            foreach (var a in nft.properties)
            {
                switch (a.Key)
                {
                    case "Name":
                        Assert.IsTrue(a.Value == "hello");
                        break;
                    case "Description":
                        Assert.IsTrue(a.Value == "desc");
                        break;
                    case "ImageURL":
                        Assert.IsTrue(a.Value == "imgURL");
                        break;
                    case "InfoURL":
                        Assert.IsTrue(a.Value == "info");
                        break;
                    case "UnlockCount":
                        Assert.IsTrue(a.Value == "111");
                        break;

                }
            }
        }

        [Test]
        public void TestNFTWrite()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            string symbol = "ATEST";
            string name = "Test";

            var sourceCode =
                @"struct someStruct
                {
                    created:timestamp;
                    creator:address;
                    royalties:number;
                    name:string;
                    description:string;
                    imageURL:string;
                    infoURL:string;
                }
                token " + symbol + @" {
                    import Runtime;
                    import Time;
                    import NFT;
                    import Map;
                    global _address:address;
                    global _owner:address;
                    global _unlockStorageMap: storage_map<number, number>;

                    property name:string = """ + name + @""";

                    nft myNFT<someStruct, number> {

                        import Call;
                        import Map;

                        property name:string {
                            return _ROM.name;
                        }

                        property description:string {
                            return _ROM.description;
                        }

                        property imageURL:string {
                            return _ROM.imageURL;
                        }

                        property infoURL:string {
                            return _ROM.infoURL;
                        }

                        property unlockCount:number {
                            local count:number := Call.interop<number>(""Map.Get"",  ""ATEST"", ""_unlockStorageMap"", _tokenID, $TYPE_OF(number));
                            return count;
                        }
                    }

                    import Call;
                    constructor(owner:address)	{
                        _address := @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;
                        _owner:= owner;
                        NFT.createSeries(owner, $THIS_SYMBOL, 0, 999, TokenSeries.Unique, myNFT);
                    }

                    public mint(dest:address):number {
                        local rom:someStruct := Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");
                        local tokenID:number := NFT.mint(_address, dest, $THIS_SYMBOL, rom, 0, 0);
                        _unlockStorageMap.set(tokenID, 0);
                        Call.interop<none>(""Map.Set"",  ""_unlockStorageMap"", tokenID, 111);
                        return tokenID;
                    }

                    public updateNFT(from:address, nftID:number) {
                        local symbol : string := $THIS_SYMBOL;
                        NFT.write(from, $THIS_SYMBOL, nftID, 1);
                    }

                    public readNFTRAM(nftID:number): number{
                        local ramInfo : number := NFT.readRAM<number>($THIS_SYMBOL, nftID);
                        return ramInfo;
                    }

                    public readName(nftID:number): string {
                        local romInfo:someStruct := NFT.readROM<someStruct>($THIS_SYMBOL, nftID);
                        return romInfo.name;
                    }

                    public readOwner(nftID:number): address {
                        local nftInfo:NFT := NFT.read($THIS_SYMBOL, nftID);
                        return nftInfo.owner;
                    }
                }";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();
            //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract..asm);
            //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract.SubModules.First().asm);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Nexus.CreateToken", keys.Address, symbol, name, 0, 0, TokenFlags.Burnable | TokenFlags.Transferable, contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            var otherKeys = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "mint", otherKeys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            var block = simulator.EndBlock().First();

            var result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            var obj = VMObject.FromBytes(result);
            var nftID = obj.AsNumber();
            Assert.IsTrue(nftID > 0);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "readName", nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            obj = VMObject.FromBytes(result);
            var nftName = obj.AsString();
            Assert.IsTrue(nftName == "hello");

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "readOwner", nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            obj = VMObject.FromBytes(result);
            var nftOwner = obj.AsAddress();
            Assert.IsTrue(nftOwner == otherKeys.Address);

            // update ram
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "updateNFT", otherKeys.Address.Text, nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);

            // Read RAM
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "readNFTRAM", nftID).
                    SpendGas(keys.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            result = block.GetResultForTransaction(tx.Hash);
            Assert.NotNull(result);
            obj = VMObject.FromBytes(result);
            var ram = obj.AsNumber();
            Assert.IsTrue(ram == 1);

            var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes(symbol), 0, new DummyLogger());
            mempool?.SetKeys(keys);

            var api = new NexusAPI(simulator.Nexus);
            api.Mempool = mempool;
            mempool.Start();

            var nft = (TokenDataResult)api.GetNFT(symbol, nftID.ToString(), true);
            foreach (var a in nft.properties)
            {
                switch (a.Key)
                {
                    case "Name":
                        Assert.IsTrue(a.Value == "hello");
                        break;
                    case "Description":
                        Assert.IsTrue(a.Value == "desc");
                        break;
                    case "ImageURL":
                        Assert.IsTrue(a.Value == "imgURL");
                        break;
                    case "InfoURL":
                        Assert.IsTrue(a.Value == "info");
                        break;
                    case "UnlockCount":
                        Assert.IsTrue(a.Value == "111");
                        break;

                }
            }
        }

        [Test]
        public void Triggers()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Time;\n" +
                "global _address:address;" +
                "global _owner:address;" +
                "constructor(owner:address)	{\n" +
                "_address := @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;\n" +
                "_owner:= owner;\n" +
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                "}\n"+
                "trigger onUpgrade(from:address)\n" +
                "{\n" +
                "    Runtime.expect(from == _address, \"invalid owner address\"\n);" +
                "	 Runtime.expect(Runtime.isWitness(from), \"invalid witness\"\n);" +
                "}\n" +
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallInterop("Runtime.UpgradeContract", keys.Address, "test", contract.script, contract.abi.ToByteArray()).
                    SpendGas(keys.Address).
                    EndScript());
            var ex = Assert.Throws<ChainException>(() => simulator.EndBlock());
            Assert.That(ex.Message, Is.EqualTo("add block @ main failed, reason: OnUpgrade trigger failed @ Runtime_UpgradeContract"));

        }

        [Test]
        public void StorageList()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Time;\n" +
                "import List;\n" +
                "global myList: storage_list<string>;\n" +
                "public getCount():number\n" +
                "{\n" +
                " return myList.count();\n" +
                "}\n" +
                "public getStuff(index:number):string \n" +
                "{\n" +
                " return myList.get(index);\n" +
                "}\n"+
                "public removeStuff(index:number) \n" +
                "{\n" +
                " myList.removeAt(index);\n" +
                "}\n" +
                "public clearStuff() \n" +
                "{\n" +
                " myList.clear();\n" +
                "}\n" +
                "public addStuff(stuff:string) \n" +
                "{\n" +
                " myList.add(stuff);\n" +
                "}\n" +
                "public replaceStuff(index:number, stuff:string) \n" +
                "{\n" +
                " myList.replace(index, stuff);\n" +
                "}\n" +
                "constructor(owner:address)	{\n" +
                "   this.addStuff(\"hello\");\n" +
                "   this.addStuff(\"world\");\n" +
                "}\n" +
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            Func<int, string> fetchListItem = (index) =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                        ScriptUtils.BeginScript().
                        AllowGas(keys.Address, Address.Null, 1, 9999).
                        CallContract("test", "getStuff", index).
                        SpendGas(keys.Address).
                        EndScript());
                var block = simulator.EndBlock().FirstOrDefault();

                var bytes = block.GetResultForTransaction(tx.Hash);
                Assert.IsTrue(bytes != null);

                var vmObj = Serialization.Unserialize<VMObject>(bytes);

                return  vmObj.AsString();
            };

            Func<int> fetchListCount = () =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                        ScriptUtils.BeginScript().
                        AllowGas(keys.Address, Address.Null, 1, 9999).
                        CallContract("test", "getCount").
                        SpendGas(keys.Address).
                        EndScript());
                var block = simulator.EndBlock().FirstOrDefault();

                var bytes = block.GetResultForTransaction(tx.Hash);
                Assert.IsTrue(bytes != null);

                var vmObj = Serialization.Unserialize<VMObject>(bytes);

                return (int)vmObj.AsNumber();
            };

            string str;
            int count;
            
            str = fetchListItem(0);
            Assert.IsTrue(str == "hello");

            str = fetchListItem(1);
            Assert.IsTrue(str == "world");

            count = fetchListCount();
            Assert.IsTrue(count == 2);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "removeStuff", 0).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();

            count = fetchListCount();
            Assert.IsTrue(count == 1);

            str = fetchListItem(0);
            Assert.IsTrue(str == "world");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "replaceStuff", 0, "A").
                    CallContract("test", "addStuff", "B").
                    CallContract("test", "addStuff", "C").
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();

            count = fetchListCount();
            Assert.IsTrue(count == 3);

            str = fetchListItem(0);
            Assert.IsTrue(str == "A");

            str = fetchListItem(1);
            Assert.IsTrue(str == "B");

            str = fetchListItem(2);
            Assert.IsTrue(str == "C");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "clearStuff").
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();

            count = fetchListCount();
            Assert.IsTrue(count == 0);
        }

        [Test]
        public void StorageMap()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Time;\n" +
                "import Map;\n" +
                "global _storageMap: storage_map<number, string>;\n" +
                "constructor(owner:address)	{\n" +
                "_storageMap.set(5, \"test1\");\n" +
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                " local test:string := _storageMap.get(5);\n" +
                " Runtime.log(\"this log: \");\n" +
                " Runtime.log(test);\n" +
                "}\n" +
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "doStuff", keys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();
        }

        public struct My_Struct
        {
            public string name;
            public BigInteger value;
        }

        [Test]
        public void StorageMapAndStruct()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "struct my_struct\n{" +
                    "name:string;\n" +
                    "value:number;\n" +
                "}\n" +
                "contract test {\n" +
                "import Runtime;\n" +
                "import Time;\n" +
                "import Map;\n" +
                "global _storageMap: storage_map<number, my_struct>;\n" +
                "public createStruct(key:number, s:string, val:number)\n" +
                "{\n" +
                "local temp: my_struct := Struct.my_struct(s, val);\n" +
                "_storageMap.set(key, temp);\n" +
                "}\n" +
                "public getStruct(key:number):my_struct\n" +
                "{\n" +
                "return _storageMap.get(key);\n" +
                "}\n" +
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "createStruct", 5, "hello", 123).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();

            var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getStruct", 5);
            var temp = vmObj.AsStruct<My_Struct>();
            Assert.IsTrue(temp.name == "hello");
            Assert.IsTrue(temp.value == 123);
        }

        [Test]
        public void AES()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Cryptography;\n" +
                "global someString: string;\n" +
                "global someSecret: string;\n" +
                "global result: string;\n" +
                "constructor(owner:address)	{\n" +
                "someString := \"somestring\";\n" +
                "someSecret := \"somesecret123456somesecret123456\";\n" +
                "local encrypted: bytes := Cryptography.AESEncrypt(someString.toBytes(), someSecret.toBytes());\n"+
                "local decrypted: bytes := Cryptography.AESDecrypt(encrypted, someSecret.toBytes());\n"+
                "result := decrypted.toString();\n" +
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                " Runtime.expect(result == someString, \"decrypted content does not equal original\");\n" +
                "}\n"+
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "doStuff", keys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();
        }

        [Test]
        public void AESAndStorageMap()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Storage;\n" +
                "import Map;\n" +
                "import Cryptography;\n" +
                "global someString: string;\n" +
                "global someSecret: string;\n" +
                "global result: string;\n" +
                "global _lockedStorageMap: storage_map<number, bytes>;\n" +
                "constructor(owner:address)	{\n" +
                "someString := \"qwerty\";\n" +
                "someSecret := \"d25a4cdb3f1b347efabb56da18069dfe\";\n" +
                "local encrypted: bytes := Cryptography.AESEncrypt(someString.toBytes(), someSecret.toBytes());\n" +
                "_lockedStorageMap.set(10, encrypted);\n" +
                "local encryptedContentBytes:bytes := _lockedStorageMap.get(10);\n" +
                "local decrypted: bytes := Cryptography.AESDecrypt(encryptedContentBytes, someSecret.toBytes());\n" +
                "result := decrypted.toString();\n" +
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                " Runtime.expect(result == someString, \"decrypted content does not equal original\");\n" +
                "}\n"+
                "}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "doStuff", keys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();
        }

        [Test]
        public void StorageMapHas()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);

            var sourceCode =
                "contract test {\n" +
                "import Runtime;\n" +
                "import Map;\n" +
                "global _storageMap: storage_map<number, string>;\n" +
                "constructor(owner:address)	{\n" +
                "_storageMap.set(5, \"test1\");\n"+
                "}\n" +
                "public doStuff(from:address)\n" +
                "{\n" +
                " local test: bool := _storageMap.has(5);\n" +
                " Runtime.expect(test, \"key 5 doesn't exist! \");\n" +
                " local test2: bool := _storageMap.has(6);\n" +
                " Runtime.expect(test2 == false, \"key 6 does exist, but should not! \");\n" +
                "}\n"+
                "}\n";
            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();
            Console.WriteLine("contract asm: " + contract.asm);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "doStuff", keys.Address).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();
        }

        [Test]
        public void ArraySimple()
        {
            // TODO make other tests also use multiline strings for source code, much more readable...
            var sourceCode = @"
contract arrays {
    import Array;

	public test(x:number):number {
		local my_array: array<number>;		
		my_array[1] := x;			
		return Array.length(my_array);		
	}	
}
";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var test = contract.abi.FindMethod("test");
            Assert.IsNotNull(test);

            vm = new TestVM(contract, storage, test);
            vm.Stack.Push(VMObject.FromObject(5));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == 1);
        }

        [Test]
        public void ArrayVariableIndex()
        {
            // TODO make other tests also use multiline strings for source code, much more readable...
            var sourceCode = @"
contract arrays {
	public test(x:number, idx:number):number {
		local my_array: array<number>;		
		my_array[idx] := x;			
		local num:number := my_array[idx];		
		return num + 1;
	}	
}
";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var test = contract.abi.FindMethod("test");
            Assert.IsNotNull(test);

            vm = new TestVM(contract, storage, test);
            vm.Stack.Push(VMObject.FromObject(2));
            vm.Stack.Push(VMObject.FromObject(5));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == 6);
        }


        [Test]
        public void StringManipulation()
        {
            var sourceCode = @"
contract arrays {
    import Array;

	public test(s:string, idx:number):string {        
		local my_array: array<number>;		
		my_array := s.toArray();	
        my_array[idx] := 42; // replace char in this index with an asterisk (ascii table 42)
		local result:string := String.fromArray(my_array);		
		return result;
	}	

	public toUpper(s:string):string 
	{        
		local my_array: array<number>;		
		
		// extract chars from string into an array
		my_array := s.toArray();	
		
		local length :number := Array.length(my_array);
		local idx :number := 0;
		
		while (idx < length) {
			local ch : number := my_array[idx];
			
			if (ch >= 97) {
				if (ch <= 122) {				
					my_array[idx] := ch - 32; 
				}
			}
						
			idx += 1;
		}
				
		// convert the array back into a unicode string
		local result:string := String.fromArray(my_array); 
		return result;
	}	

}
";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var test = contract.abi.FindMethod("test");
            Assert.IsNotNull(test);

            vm = new TestVM(contract, storage, test);
            vm.Stack.Push(VMObject.FromObject(2));
            vm.Stack.Push(VMObject.FromObject("ABCD"));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            var result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "AB*D");

            var toUpper = contract.abi.FindMethod("toUpper");
            Assert.IsNotNull(toUpper);

            vm = new TestVM(contract, storage, toUpper);
            vm.Stack.Push(VMObject.FromObject("abcd"));
            state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "ABCD");
        }

        // add simplified version of that test
        //[Test]
        //public void TestGHOST()
        //{
        //    var keys = PhantasmaKeys.Generate();
        //    var keys2 = PhantasmaKeys.Generate();

        //    var nexus = new Nexus("simnet", null, null);
        //    nexus.SetOracleReader(new OracleSimulator(nexus));
        //    var simulator = new NexusSimulator(nexus, keys, 1234);
        //    var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger());
        //    mempool?.SetKeys(keys);

        //    var api = new NexusAPI(simulator.Nexus);
        //    api.Mempool = mempool;
        //    mempool.Start();
        //    var sourceCode = System.IO.File.ReadAllLines("/home/merl/source/phantasma/GhostMarketContractPhantasma/GHOST.tomb");
        //    var parser = new Compiler();
        //    var contract = parser.Process(sourceCode).First();
        //    //Console.WriteLine("contract asm: " + contract.asm);
        //    //System.IO.File.WriteAllText(@"GHOST_series.asm", contract.SubModules.First().asm);

        //    simulator.BeginBlock();
        //    simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
        //            () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
        //            .CallInterop("Nexus.CreateToken", keys.Address, "GHOST", "GHOST", new BigInteger(10000), new BigInteger(0),
        //                TokenFlags.Transferable|TokenFlags.Burnable|TokenFlags.Finite, contract.script, contract.abi.ToByteArray())
        //            .SpendGas(keys.Address)
        //            .EndScript());
        //    simulator.EndBlock();

        //    var token = (TokenResult)api.GetToken("GHOST");
        //    Console.WriteLine("id: " + token.ToString());
        //    Console.WriteLine("address: " + token.address);

        //    simulator.BeginBlock();
        //    simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
        //            () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.FromText(token.address), 1, 9999)
        //            .CallContract("GHOST", "mintToken", 0, 1, 1,
        //                keys.Address, 0, "GHOST", 1, "testnft", "desc1234567890", 1,
        //                "0", "0", "", "", "", "", "", "", "", 0, "", new Timestamp(1), "", 0)
        //            .SpendGas(keys.Address)
        //            .EndScript());
        //    simulator.EndBlock();

        //    Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        //    var nft = (TokenDataResult)api.GetNFT("GHOST", "80807712912753409015029052615541912663228133032695758696669246580757047529373", true);
        //    Console.WriteLine("nft series: " + nft.series);
        //}

        /*[Test]
        public void TestCROWN()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);
            var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger());
            mempool?.SetKeys(keys);

            var api = new NexusAPI(simulator.Nexus);
            api.Mempool = mempool;
            mempool.Start();

            var token = (TokenResult)api.GetToken("CROWN");
            Console.WriteLine("id: " + token.ToString());
            Console.WriteLine("address: " + token.address);

            simulator.TimeSkipDays(200);
            var nft = (TokenDataResult)api.GetNFT("CROWN", "64648043722874601761586352284082823113174122931185981250820896676646424691598", true);
            Console.WriteLine("nft series: " + nft.properties.ToString());
            foreach (var a in nft.properties)
            {
                Console.WriteLine($"res {a.Key}:{a.Value}");

            }
        }*/
    }
}
