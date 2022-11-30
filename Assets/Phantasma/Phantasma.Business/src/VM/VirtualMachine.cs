using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Performance;

namespace Phantasma.Business.VM
{

    public abstract class VirtualMachine: IVirtualMachine
    {
        public const int DefaultRegisterCount = 32;

        public const int MaxRegisterCount = 32;

        public readonly static string EntryContextName = "entry";

        public readonly static string StakeContextName = "stake";

        public readonly static string GasContextName = "gas";

        public readonly static string ExchangeContextName = "exchange";

        private readonly ExecutionContext entryContext;

        public ExecutionContext EntryContext => entryContext;


        public Stack<VMObject> Stack { get; } = new Stack<VMObject>();

        public byte[] EntryScript { get; }

        private Address entryAddress;
        public Address EntryAddress { 
            get
            {
                return this.entryAddress;
            }

            set
            {
                this.entryAddress = value;
            } 
        }

        public ExecutionContext CurrentContext { get; set; }
        public ExecutionContext PreviousContext { get; set; }

        private Stack<Address> _activeAddresses = new Stack<Address>();
        public Stack<Address> ActiveAddresses => _activeAddresses;

        private Dictionary<string, ExecutionContext> _contextMap = new Dictionary<string, ExecutionContext>();

        private readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public Stack<ExecutionFrame> Frames { get { return frames; } }
        public ExecutionFrame CurrentFrame { get; set; }

        public VirtualMachine(byte[] script, uint offset, string contextName)
        {
            Throw.IfNull(script, nameof(script));

            this.EntryAddress = Address.FromHash(script);
            this._activeAddresses.Push(EntryAddress);

            if (contextName == null)
            {
                contextName = EntryContextName;
            }

            this.entryContext = new ScriptContext(contextName, script, offset);
            RegisterContext(EntryContextName, this.entryContext);

            PreviousContext = entryContext;

            this.EntryScript = script;
        }

        public void RegisterContext(string contextName, ExecutionContext context)
        {
            _contextMap[contextName] = context;
        }

        public abstract ExecutionState ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(string contextName);

        public virtual ExecutionState Execute()
        {
            return SwitchContext(entryContext, 0);
        }

        #region FRAMES

        // instructionPointer is the location to jump after the frame is popped!
        public void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount)
        {
            var frame = new ExecutionFrame(this, instructionPointer, context, registerCount);
            frames.Push(frame);
            this.CurrentFrame = frame;
        }

        public virtual uint PopFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            var oldFrame = frames.Pop();
            var instructionPointer = CurrentFrame.Offset;

            this.CurrentFrame = frames.Peek();
            SetCurrentContext(CurrentFrame.Context);

            return instructionPointer;
        }

        public ExecutionFrame PeekFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            // TODO do this without pop/push
            var temp = frames.Pop();
            var result = frames.Peek();
            frames.Push(temp);

            return result;
        }

        public void SetCurrentContext(ExecutionContext context)
        {
            if (context == null)
            {
                throw new VMException(this, "SetCurrentContext failed, context can't be null");
            }

            this.CurrentContext = context;
        }

        public virtual ExecutionContext FindContext(string contextName)
        {
            if (_contextMap.ContainsKey(contextName))
            {
                return _contextMap[contextName];
            }

            var result = LoadContext(contextName);
            if (result == null)
            {
                return null;
            }

            _contextMap[contextName] = result;

            return result;
        }

        public virtual ExecutionState ValidateOpcode(Opcode opcode)
        {
            return ExecutionState.Running;
        }

        public virtual ExecutionState SwitchContext(ExecutionContext context, uint instructionPointer)
        {
            if (context == null)
            {
                throw new VMException(this, "SwitchContext failed, context can't be null");
            }

            var tempContext = PreviousContext;
            PreviousContext = CurrentContext;
            SetCurrentContext(context);
            PushFrame(context, instructionPointer, DefaultRegisterCount);

            _activeAddresses.Push(context.Address);

            var result = context.Execute(this.CurrentFrame, this.Stack);

            PreviousContext = tempContext;

            var temp = _activeAddresses.Pop();
            if (temp != context.Address)
            {
                throw new VMException(this, "VM implementation bug detected: address stack");
            }

            return result;
            
        }
        #endregion

        public virtual string GetDumpFileName()
        {
            return "vm.txt";
        }

        public abstract void DumpData(List<string> lines);

        public void Expect(bool condition, string description)
        {
            if (condition)
            {
                return;
            }

            var callingFrame = new StackFrame(1);
            var method = callingFrame.GetMethod();

            description = $"{description} @ {method.Name}";

            throw new VMException(this, description);
        }

    }

    public class VMException : Exception
    {
        public IVirtualMachine vm;

        public static string Header(string s)
        {
            return $"*********{s}*********";
        }

        public VMException(IVirtualMachine vm, string msg) : base(msg)
        {
            this.vm = vm;
#if DEBUG
            var fileName = vm.GetDumpFileName();
            if (fileName != null)
            {
                DumpToFile(fileName);
            }
#endif
        }

        private void DumpToFile(string fileName)
        {
            var temp = new Disassembler(vm.EntryScript);

            var lines = new List<string>();

            lines.Add("Exception: "+this.Message);

            if (vm.CurrentContext is ScriptContext sc)
            {
                lines.Add(Header("CURRENT OFFSET"));
                lines.Add(sc.InstructionPointer.ToString());
                lines.Add("");
            }

            lines.Add(Header("STACK"));
            var stack = vm.Stack.ToArray();
            for (int i = 0; i < stack.Length; i++)
            {
                lines.Add(stack[i].ToString());
            }
            lines.Add("");

            lines.Add(Header("FRAMES"));
            int ct = 0;
            var frames = vm.Frames.ToArray();
            foreach (var frame in frames)
            {
                if (ct > 0)
                {
                    lines.Add("");
                }

                lines.Add("Active = " + (frame == vm.CurrentFrame).ToString());
                lines.Add("Entry Offset = " + frame.Offset.ToString());
                lines.Add("Registers:");
                int ri = 0;
                foreach (var reg in frame.Registers)
                {
                    if (reg.Type != VMType.None)
                    {
                        lines.Add($"\tR{ri} = {reg}");
                    }

                    ri++;
                }
                ct++;
            }
            lines.Add("");

            var disasm = temp.Instructions.Select(inst => inst.ToString());
            lines.Add(Header("DISASM"));
            lines.AddRange(disasm);
            lines.Add("");

            vm.DumpData(lines);

            var dirName = Directory.GetCurrentDirectory() + "/Dumps/";
            Directory.CreateDirectory(dirName);

            var path = dirName + fileName;
            System.Diagnostics.Debug.WriteLine("Dumped VM data: " + path);
            File.WriteAllLines(path, lines.ToArray());
        }
    }
}
