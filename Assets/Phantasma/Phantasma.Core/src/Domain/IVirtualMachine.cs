using System.Collections.Generic;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain
{
    public interface IVirtualMachine
    {
        public Stack<VMObject> Stack { get; }

        public byte[] EntryScript { get; }

        public Address EntryAddress { get; set; }

        public ExecutionContext CurrentContext { get; set; }

        public ExecutionContext PreviousContext { get; set; }

        public ExecutionFrame CurrentFrame { get; set; }

        public Stack<ExecutionFrame> Frames { get; }

        public void RegisterContext(string contextName, ExecutionContext context);

        public ExecutionState ExecuteInterop(string method);

        public abstract ExecutionContext LoadContext(string contextName);

        public ExecutionState Execute();

        public void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount);

        public uint PopFrame();

        public ExecutionFrame PeekFrame();

        public void SetCurrentContext(ExecutionContext context);

        public ExecutionContext FindContext(string contextName);

        public ExecutionState ValidateOpcode(Opcode opcode);

        public ExecutionState SwitchContext(ExecutionContext context, uint instructionPointer);

        public string GetDumpFileName();

        public void DumpData(List<string> lines);

        public void Expect(bool condition, string description);

    }
}
