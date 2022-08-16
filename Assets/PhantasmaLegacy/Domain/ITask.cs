using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Numerics;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public enum TaskFrequencyMode
    {
        Always,
        Time,
        Blocks,
    }

    public enum TaskResult
    {
        Running,
        Halted,
        Crashed,
        Skipped,
    }

    public interface ITask
    {
        BigInteger ID { get; }
        bool State { get; }
        Address Owner { get; }
        string ContextName { get; }
        string Method { get; }
        uint Frequency { get; }
        uint Delay { get; }        
        TaskFrequencyMode Mode { get; }
        BigInteger GasLimit { get; }
        BigInteger Height { get; }
    }


}

