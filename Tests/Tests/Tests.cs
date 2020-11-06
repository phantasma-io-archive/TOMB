using NUnit.Framework;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Tomb.Compiler;
using Phantasma.VM;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
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
        public void TestCounter()
        {
            var sourceCode =
            "contract test{\n" +
            "global counter: number;\n" +
            "constructor(owner:address)	{\n" +
            "counter:= 0;}\n" +
            "public increment(){\n" +
            "if (counter < 0){\n" +
            "throw \"invalid state\";}\n" +
            "counter += 1;\n" +
            "}}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
            Assert.IsNotNull(constructor);

            var keys = PhantasmaKeys.Generate();

            // call constructor
            vm = new TestVM(contract, storage, constructor);
            vm.ThrowOnFault = true;
            vm.Stack.Push(VMObject.FromObject(keys.Address));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);


            // call increment
            var increment = contract.abi.FindMethod("increment");
            Assert.IsNotNull(increment);

            vm = new TestVM(contract, storage, increment);
            vm.ThrowOnFault = true;
            result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(storage.Count == 1);
        }

        [Test]
        public void TestStrings()
        {
            var sourceCode =
            "contract test{\n" +
            "global name: string;\n" +
            "constructor(owner:address)	{\n" +
            "name:= \"hello\";\n" +
            /*"if (counter < 0){\n" +
            "throw \"invalid state\";}\n" +
            "counter += 1;\n" +*/
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
        }
    }
}