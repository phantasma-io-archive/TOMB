using NUnit.Framework;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Utils;
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
            private Dictionary<string, Func<ExecutionFrame, ExecutionState>> _interops = new Dictionary<string, Func<ExecutionFrame, ExecutionState>>();
            private Func<string, ExecutionContext> _contextLoader;
            private Dictionary<string, ScriptContext> contexts;
            private Dictionary<byte[], byte[]> storage;

            public TestVM(byte[] script, Dictionary<byte[], byte[]> storage) : base(script)
            {
                this.storage = storage;
                RegisterContextLoader(ContextLoader);
                RegisterInterop("Data.Set", Data_Set);
                RegisterInterop("Data.Get", Data_Get);
                RegisterInterop("Data.Delete", Data_Delete);
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

            public void RegisterInterop(string method, Func<ExecutionFrame, ExecutionState> callback)
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
                    return _interops[method](this.CurrentFrame);
                }

                throw new NotImplementedException();
            }

            public override ExecutionContext LoadContext(string contextName)
            {
                if (_contextLoader != null)
                {
                    return _contextLoader(contextName);
                }

                throw new NotImplementedException();
            }

            public override void DumpData(List<string> lines)
            {
                // do nothing
            }

            public void Expect(bool condition, string description)
            {
                if (condition)
                {
                    return;
                }

                throw new VMException(this, description);
            }


            private ExecutionState Data_Get(ExecutionFrame frame)
            {
                var key = this.Stack.Pop();
                var key_bytes = key.AsByteArray();

                var type_obj = this.Stack.Pop();
                var vmType = type_obj.AsEnum<VMType>();

                this.Expect(key_bytes.Length > 0, "invalid key");

                var value_bytes = storage[key_bytes];
                var val = new VMObject();
                val.SetValue(value_bytes, vmType);
                this.Stack.Push(val);

                return ExecutionState.Running;
            }

            private ExecutionState Data_Set(ExecutionFrame frame)
            {
                var key = this.Stack.Pop();
                var key_bytes = key.AsByteArray();

                var val = this.Stack.Pop();
                var val_bytes = val.AsByteArray();

                this.Expect(key_bytes.Length > 0, "invalid key");

                var firstChar = (char)key_bytes[0];
                this.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

                this.storage[key_bytes] = val_bytes;

                return ExecutionState.Running;
            }

            private ExecutionState Data_Delete(ExecutionFrame frame)
            {
                var key = this.Stack.Pop();
                var key_bytes = key.AsByteArray();

                this.Expect(key_bytes.Length > 0, "invalid key");

                var firstChar = (char)key_bytes[0];
                this.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

                this.storage.Remove(key_bytes);

                return ExecutionState.Running;
            }


        }

        [Test]
        public void TestCounter()
        {
            var sourceCode =
            "contract test{\n" +
            "global counter: number;\n" +
            "constructor()	{\n" +
            "counter:= 0;}\n" +
            "method increment(){\n" +
            "if (counter < 0){\n" +
            "throw \"invalid state\";}\n" +
            "counter += 1;\n" +
            "}}\n";

            var parser = new Compiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;
            
            /*vm = new TestVM(script, storage);
            vm.Stack.Push(VMObject.FromObject("test()"));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);*/

            vm = new TestVM(contract.script, storage);
            vm.Stack.Push(VMObject.FromObject("increment"));
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);
        }
    }
}