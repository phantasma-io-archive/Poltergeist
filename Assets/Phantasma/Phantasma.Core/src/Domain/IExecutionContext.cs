using System.Collections.Generic;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain
{
    public enum ExecutionState
    {
        Running,
        Break,
        Fault,
        Halt
    }

    public interface IExecutionContext
    {
        public string Name { get; }

        public Address Address { get; }

        public abstract ExecutionState Execute(IExecutionFrame frame, Stack<VMObject> stack);
    }
}
