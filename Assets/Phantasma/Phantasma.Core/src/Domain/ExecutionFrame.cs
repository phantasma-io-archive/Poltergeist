using System;

namespace Phantasma.Core.Domain
{
    public class ExecutionFrame
    {
        public VMObject[] Registers { get; }

        /// <summary>
        ///     Current instruction pointer **before** the frame was entered.
        /// </summary>
        public uint Offset { get; }
        
        public ExecutionContext Context { get; }
        public IVirtualMachine VM { get; }

        public ExecutionFrame(IVirtualMachine vm, uint offset, ExecutionContext context, int registerCount)
        {
            VM = vm;
            Offset = offset;
            Context = context;

            Registers = new VMObject[registerCount];

            for (int i = 0; i < registerCount; i++)
            {
                Registers[i] = new VMObject();
            }
        }

        public VMObject GetRegister(int index)
        {
            if (index < 0 || index >= Registers.Length)
            {
                throw new ArgumentException("Invalid index");
            }

            return Registers[index];
        }
    }
}
