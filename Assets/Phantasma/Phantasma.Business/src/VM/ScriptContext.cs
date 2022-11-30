using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Phantasma.Core;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Performance;

namespace Phantasma.Business.VM
{
    public class ScriptContext : ExecutionContext
    {
        public static readonly byte[] EmptyScript = new byte[] { (byte)Opcode.RET };

        public byte[] Script { get; private set; }
        public override string Name => _name;

        public uint InstructionPointer { get; private set; }

        private string _name;

        private ExecutionState _state;
        public string Error { get; private set; }

        private Opcode opcode;

        public ScriptContext(string name, byte[] script, uint offset)
        {
            this._name = name;
            this._state = ExecutionState.Running;
            this.Script = script;
            this.InstructionPointer = offset;
            this.opcode = Opcode.NOP;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            while (_state == ExecutionState.Running)
            {
                this.Step(ref frame, stack);
            }

            return _state;
        }

#region IO
        private byte Read8()
        {
            Throw.If(InstructionPointer >= this.Script.Length, $"Outside of script range => {InstructionPointer} / {this.Script.Length}");

            var result = this.Script[InstructionPointer];
            InstructionPointer++;
            return result;
        }

        private ushort Read16()
        {
            var a = Read8();
            var b = Read8();
            return (ushort)(a + (b << 8));
        }

        private uint Read32()
        {
            var a = Read8();
            var b = Read8();
            var c = Read8();
            var d = Read8();
            return (uint)(a + (b << 8) + (c << 16) + (d << 24));
        }

        private ulong Read64()
        {
            var a = Read8();
            var b = Read8();
            var c = Read8();
            var d = Read8();
            var e = Read8();
            var f = Read8();
            var g = Read8();
            var h = Read8();
            return (ulong)(a + (b << 8) + (c << 16) + (d << 24) + (e << 32) + (f << 40) + (g << 48) + (g << 56));
        }

        private ulong ReadVar(ulong max)
        {
            byte n = Read8();

            ulong val;

            switch (n)
            {
                case 0xFD: val = Read16(); break;
                case 0xFE: val = Read32(); break;
                case 0xFF: val = Read64(); break;
                default: val = n; break;
            }

            Throw.If(val > max, "Input exceed max");

            return val;
        }

        private byte[] ReadBytes(int length)
        {
            Throw.If(InstructionPointer + length >= this.Script.Length, "Outside of range");

            var result = new byte[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = this.Script[InstructionPointer];
                InstructionPointer++;
            }

            return result;
        }
#endregion

        private void Expect(bool condition, string error)
        {
            if (!condition)
            {
                throw new Exception($"Script execution failed: {error} @ {opcode} : {InstructionPointer}");
            }
        }

        private void SetState(ExecutionState state, string error = null)
        {
            if (state == ExecutionState.Fault && string.IsNullOrEmpty(error))
            {
                error = "Generic Error";
            }

            this._state = state;
            this.Error = error;
        }

        public void Step(ref ExecutionFrame frame, Stack<VMObject> stack)
        {
            try
            {
                opcode = (Opcode)Read8();

                frame.VM.ValidateOpcode(opcode);

                switch (opcode)
                {
                    case Opcode.NOP:
                        {
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.MOVE:
                        {
                            Move(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.COPY:
                        {
                            Copy(ref frame);
                            break;
                        }

                        // args: byte dst_reg, byte type, var length, var data_bytes
                    case Opcode.LOAD:
                        {
                            Load(ref frame);
                            break;
                        }

                        // args: byte src_reg, dst_reg, byte type
                    case Opcode.CAST:
                        {
                            Cast(ref frame);
                            break;
                        }

                        // args: byte src_reg
                    case Opcode.PUSH:
                        {
                            Push(ref frame, stack);
                            break;
                        }

                        // args: byte dest_reg
                    case Opcode.POP:
                        {
                            Pop(ref frame, stack);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.SWAP:
                        {
                            Swap(ref frame);
                            break;
                        }

                        // args: ushort offset, byte regCount
                    case Opcode.CALL:
                        {
                            Call(ref frame);
                            break;
                        }

                        // args: byte srcReg
                    case Opcode.EXTCALL:
                        {
                            Extcall(ref frame);
                            break;
                        }

                        // args: ushort offset, byte src_reg
                        // NOTE: JMP only has offset arg, not the rest
                    case Opcode.JMP:
                    case Opcode.JMPIF:
                    case Opcode.JMPNOT:
                        {
                            Jump(ref frame);
                            break;
                        }

                        // args: var length, var bytes
                    case Opcode.THROW:
                        {
                            OPThrow(ref frame);
                            break;
                        }

                        // args: none
                    case Opcode.RET:
                        {
                            Ret(ref frame);
                            return;
                        }

                        // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.CAT:
                        {
                            Cat(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg, var index, var length
                    case Opcode.RANGE:
                        {
                            Range(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg, var length
                    case Opcode.LEFT:
                        {
                            Left(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg, byte length
                    case Opcode.RIGHT:
                        {
                            Right(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.SIZE:
                        {
                            Size(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.COUNT:
                        {
                            Count(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.NOT:
                        {
                            Not(ref frame);
                            break;
                        }

                        // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.AND:
                    case Opcode.OR:
                    case Opcode.XOR:
                        {
                            Logic(ref frame);
                            break;
                        }

                        // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.EQUAL:
                        {
                            Equal(ref frame);
                            break;
                        }

                        // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.LT:
                    case Opcode.GT:
                    case Opcode.LTE:
                    case Opcode.GTE:
                        {
                            Comparison(ref frame);
                            break;
                        }

                        // args: byte reg
                    case Opcode.INC:
                        {
                            Increment(ref frame);
                            break;
                        }

                        // args: byte reg
                    case Opcode.DEC:
                        {
                            Decrement(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.SIGN:
                        {
                            Sign(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.NEGATE:
                        {
                            Negate(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg
                    case Opcode.ABS:
                        {
                            ABS(ref frame);
                            break;
                        }

                        // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.ADD:
                    case Opcode.SUB:
                    case Opcode.MUL:
                    case Opcode.DIV:
                    case Opcode.MOD:
                    case Opcode.SHR:
                    case Opcode.SHL:
                    case Opcode.MIN:
                    case Opcode.MAX:
                    case Opcode.POW:
                        {
                            Operations(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg, byte key
                    case Opcode.PUT:
                        {
                            Put(ref frame);
                            break;
                        }

                        // args: byte src_reg, byte dest_reg, byte key
                    case Opcode.GET:
                        {
                            Get(ref frame);
                            break;
                        }

                        // args: byte dest_reg
                    case Opcode.CLEAR:
                        {
                            Clear(ref frame);
                            break;
                        }

                        // args: byte dest_reg, var key
                    case Opcode.CTX:
                        {
                            Context(ref frame);
                            break;
                        }


                        // args: byte src_reg
                    case Opcode.SWITCH:
                        {
                            OPSwitch(ref frame);
                            break;
                        }

                        // args: byte src_reg dst_reg
                    case Opcode.UNPACK:
                        {
                            Unpack(ref frame);
                            break;
                        }

                    case Opcode.DEBUG:
                        {
                            break; // put here a breakpoint for debugging
                        }

                    default:
                        {
                            throw new VMException(frame.VM, $"Unknown VM opcode: {(int)opcode}");
                        }
                }
            }
            catch (Exception ex)
            {

                if (ex is NullReferenceException)
                {
                    //Log.Error("Critical exception: " + ex);
                    Environment.Exit(-1);
                }

                ex = ex.ExpandInnerExceptions();

                Trace.WriteLine(ex.ToString());

                if (ex is not VMException)
                {
                    ex = new VMException(frame.VM, ex.Message);
                }

                //Log.Debug("Exception during execution in vm: " + ex);

                SetState(ExecutionState.Fault);

                throw ex;
            }
        }

#region Handle States

        /// <summary>
        /// Opcode - MOVE -> Operation Move
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Move(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            frame.Registers[dst] = frame.Registers[src];
            frame.Registers[src] = new VMObject();
        }

        /// <summary>
        /// Opcode - COPY -> Operation Copy
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Copy(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            frame.Registers[dst].Copy(frame.Registers[src]);
        }

        /// <summary>
        /// Opcode - LOAD -> Operation Load
        /// args -> byte dst_reg, byte type, var length, var data_bytes
        /// </summary>
        /// <param name="frame"></param>
        private void Load(ref ExecutionFrame frame)
        {
            var dst = Read8();
            var type = (VMType)Read8();
            var len = (int)ReadVar(0xFFFF);

            Expect(dst < frame.Registers.Length, "invalid dst register");

            var bytes = ReadBytes(len);
            frame.Registers[dst].SetValue(bytes, type);
        }

        /// <summary>
        /// Opcode - CAST -> Operation Cast
        /// args -> byte src_reg, dst_reg, byte type
        /// </summary>
        /// <param name="frame"></param>
        private void Cast(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var type = (VMType)Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[src];
            val = VMObject.CastTo(val, type);

            frame.Registers[dst] = val;
        }

        /// <summary>
        /// Opcode - PUSH -> Operation Push
        /// args -> byte src_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Push(ref ExecutionFrame frame, Stack<VMObject> stack)
        {
            var src = Read8();
            Expect(src < frame.Registers.Length, "invalid src register");

            var val = frame.Registers[src];

            var temp = new VMObject();
            temp.Copy(val);
            stack.Push(temp);
        }

        /// <summary>
        /// Opcode - POP -> Operation Pop
        /// args -> byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Pop(ref ExecutionFrame frame,  Stack<VMObject> stack)
        {
            var dst = Read8();

            Expect(stack.Count > 0, "stack is empty");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = stack.Pop();
            frame.Registers[dst] = val;
        }

        /// <summary>
        /// Opcode - SWAP -> Operation Swap
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Swap(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var temp = frame.Registers[src];
            frame.Registers[src] = frame.Registers[dst];
            frame.Registers[dst] = temp;
        }

        /// <summary>
        /// Opcode - CALL -> Operation Call
        /// args -> ushort offset, byte regCount
        /// </summary>
        /// <param name="frame"></param>
        private void Call(ref ExecutionFrame frame)
        {
            var count = Read8();
            var ofs = Read16();

            Expect(ofs < this.Script.Length, "invalid jump offset");
            Expect(count >= 1, "at least 1 register required");
            Expect(count <= VirtualMachine.MaxRegisterCount, "invalid register allocs");

            frame.VM.PushFrame(this, InstructionPointer, count);
            frame = frame.VM.CurrentFrame;

            InstructionPointer = ofs;
        }

        /// <summary>
        /// Opcode - EXTCALL -> Operation External Call
        /// args -> byte srcReg
        /// </summary>
        /// <param name="frame"></param>
        private void Extcall(ref ExecutionFrame frame)
        {
            using (var m = new ProfileMarker("EXTCALL"))
            {
                var src = Read8();
                Expect(src < frame.Registers.Length, "invalid src register");

                var method = frame.Registers[src].AsString();

                var state = frame.VM.ExecuteInterop(method);
                if (state != ExecutionState.Running)
                {
                    throw new VMException(frame.VM, "VM extcall failed: " + method);
                }
            }
        }

        /// <summary>
        /// Opcode - JMP -> Operation Jump
        /// Opcode - JMPIF -> Operation Jump If
        /// Opcode - JMPNOT -> Operation Jump Not
        /// args -> ushort offset, byte src_reg
        /// NOTE -> JMP only has offset arg, not the rest
        /// </summary>
        /// <param name="frame"></param>
        private void Jump(ref ExecutionFrame frame)
        {
            bool shouldJump;

            if (opcode == Opcode.JMP)
            {
                shouldJump = true;
            }
            else
            {
                var src = Read8();
                Expect(src < frame.Registers.Length, "invalid src register");

                shouldJump = frame.Registers[src].AsBool();

                if (opcode == Opcode.JMPNOT)
                {
                    shouldJump = !shouldJump;
                }
            }

            var newPos = (short)Read16();

            Expect(newPos >= 0, "jump offset can't be negative value");
            Expect(newPos < this.Script.Length, "trying to jump outside of script bounds");

            if (shouldJump)
            {
                InstructionPointer = (uint)newPos;
            }

        }

        /// <summary>
        /// Opcode - THROW -> Operation Throw
        /// args -> var length, var bytes
        /// </summary>
        /// <param name="frame"></param>
        private void OPThrow(ref ExecutionFrame frame)
        {
            var src = Read8();

            Expect(src < frame.Registers.Length, "invalid exception register");

            var exception = frame.Registers[src];
            var exceptionMessage = exception.AsString();

            throw new VMException(frame.VM, exceptionMessage);
        }

        /// <summary>
        /// Opcode - RET -> Operation Return
        /// args -> none
        /// </summary>
        /// <param name="frame"></param>
        private void Ret(ref ExecutionFrame frame)
        {
            if (frame.VM.Frames.Count > 1)
            {
                var temp = frame.VM.PeekFrame();

                if (temp.Context.Name == this.Name)
                {
                    InstructionPointer = frame.VM.PopFrame();
                    frame = frame.VM.CurrentFrame;
                }
                else
                {
                    SetState(ExecutionState.Halt);
                }
            }
            else
            {
                SetState(ExecutionState.Halt);
            }
        }

        /// <summary>
        /// Opcode - CAT -> Operation Concatenation
        /// args -> byte src_a_reg, byte src_b_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Cat(ref ExecutionFrame frame)
        {
            var srcA = Read8();
            var srcB = Read8();
            var dst = Read8();

            Expect(srcA < frame.Registers.Length, "invalid srcA register");
            Expect(srcB < frame.Registers.Length, "invalid srcB register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var A = frame.Registers[srcA];
            var B = frame.Registers[srcB];

            if (!A.IsEmpty)
            {
                if (B.IsEmpty)
                {
                    frame.Registers[dst].Copy(A);
                }
                else
                {
                    if (A.Type != B.Type)
                    {
                        throw new VMException(frame.VM, "Invalid cast during concat opcode");
                    }

                    var bytesA = A.AsByteArray();
                    var bytesB = B.AsByteArray();

                    var result = new byte[bytesA.Length + bytesB.Length];
                    Array.Copy(bytesA, result, bytesA.Length);
                    Array.Copy(bytesB, 0, result, bytesA.Length, bytesB.Length);

                    VMType type = A.Type;
                    frame.Registers[dst].SetValue(result, type);
                }
            }
            else
            {
                if (B.IsEmpty)
                {
                    frame.Registers[dst] = new VMObject();
                }
                else
                {
                    frame.Registers[dst].Copy(B);
                }
            }
        }

        /// <summary>
        /// Opcode - RANGE -> Operation Range
        /// args -> byte src_reg, byte dest_reg, var index, var length
        /// </summary>
        /// <param name="frame"></param>
        private void Range(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var index = (int)ReadVar(0xFFFF);
            var len = (int)ReadVar(0xFFFF);

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var src_type = frame.Registers[src].Type;
            var src_array = frame.Registers[src].AsByteArray();
            Expect(len <= src_array.Length, "invalid length");

            Expect(index >= 0, "invalid negative index");

            var end = index + len;
            if (end > src_array.Length)
            {
                len = src_array.Length - index;

                Expect(len > 0, "empty range");
            }

            var result = new byte[len];

            Array.Copy(src_array, index, result, 0, len);

            frame.Registers[dst].SetValue(result, src_type);
        }

        /// <summary>
        /// Opcode - LEFT -> Operation Left
        /// args -> byte src_reg, byte dest_reg, var length
        /// </summary>
        /// <param name="frame"></param>
        private void Left(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var len = (int)ReadVar(0xFFFF);

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var src_array = frame.Registers[src].AsByteArray();
            Expect(len <= src_array.Length, "invalid length");

            var result = new byte[len];

            Array.Copy(src_array, result, len);

            frame.Registers[dst].SetValue(result, VMType.Bytes);
        }

        /// <summary>
        /// Opcode - RIGHT -> Operation Right
        /// args -> byte src_reg, byte dest_reg, byte length
        /// </summary>
        /// <param name="frame"></param>
        private void Right(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var len = (int)ReadVar(0xFFFF);

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var src_array = frame.Registers[src].AsByteArray();
            Expect(len <= src_array.Length, "invalid length register");

            var ofs = src_array.Length - len;

            var result = new byte[len];
            Array.Copy(src_array, ofs, result, 0, len);

            frame.Registers[dst].SetValue(result, VMType.Bytes);
        }

        /// <summary>
        /// Opcode - SIZE -> Operation Size
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Size(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            int size;

            var src_val = frame.Registers[src];

            switch (src_val.Type)
            {
                case VMType.String:
                    size = src_val.AsString().Length;
                    break;

                case VMType.Timestamp:
                case VMType.Number:
                case VMType.Enum:
                case VMType.Bool:
                    size = 1;
                    break;

                case VMType.None:
                    size = 0;
                    break;

                default:
                    var src_array= src_val.AsByteArray();
                    size = src_array.Length;
                    break;
            }

            frame.Registers[dst].SetValue(size);
        }

        /// <summary>
        /// Opcode - COUNT -> Operation Count
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Count(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[src];
            int count;

            switch (val.Type)
            {
                case VMType.Struct:
                    {
                        var children = val.GetChildren();
                        count = children.Count;
                        break;
                    }

                case VMType.None:
                    count = 0;
                    break;

                default: count = 1; break;
            }

            frame.Registers[dst].SetValue(count);
        }

        /// <summary>
        /// Opcode - NOT -> Operation NOT gate
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Not(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[src].AsBool();

            frame.Registers[dst].SetValue(!val);
        }

        /// <summary>
        /// Opcode - AND -> Operation And Gate
        /// Opcode - OR -> Operation Or Gate
        /// Opcode - XOR -> Operation Xor Gate
        /// args -> byte src_a_reg, byte src_b_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Logic(ref ExecutionFrame frame)
        {
            var srcA = Read8();
            var srcB = Read8();
            var dst = Read8();

            Expect(srcA < frame.Registers.Length, "invalid srcA register");
            Expect(srcB < frame.Registers.Length, "invalid srcB register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var valA = frame.Registers[srcA];
            var valB = frame.Registers[srcB];

            switch (valA.Type)
            {
                case VMType.Bool:
                    {
                        Expect(valB.Type == VMType.Bool, $"expected {valA.Type} for logical op");


                        var a = valA.AsBool();
                        var b = valB.AsBool();

                        bool result;
                        switch (opcode)
                        {
                            case Opcode.AND: result = (a && b); break;
                            case Opcode.OR: result = (a || b); break;
                            case Opcode.XOR: result = (a ^ b); break;
                            default:
                                             {
                                                 SetState(ExecutionState.Fault);
                                                 return;
                                             }
                        }

                        frame.Registers[dst].SetValue(result);
                        break;
                    }

                case VMType.Enum:
                    {
                        Expect(valB.Type == VMType.Enum, $"expected {valA.Type} for flag op");

                        var numA = valA.AsNumber();
                        var numB = valB.AsNumber();

                        Expect(numA.GetBitLength() <= 32, "too many bits");
                        Expect(numB.GetBitLength() <= 32, "too many bits");

                        var a = (uint)numA;
                        var b = (uint)numB;

                        if (opcode != Opcode.AND) {
                            SetState(ExecutionState.Fault);
                        }

                        bool result = (a & b) != 0;

                        frame.Registers[dst].SetValue(result);
                        break;

                    }

                case VMType.Number:
                    {
                        Expect(valB.Type == VMType.Number, $"expected {valA.Type} for logical op");

                        var numA = valA.AsNumber();
                        var numB = valB.AsNumber();

                        Expect(numA.GetBitLength() <= 64, "too many bits");
                        Expect(numB.GetBitLength() <= 64, "too many bits");

                        var a = (long)numA;
                        var b = (long)numB;

                        BigInteger result;
                        switch (opcode)
                        {
                            case Opcode.AND: result = (a & b); break;
                            case Opcode.OR: result = (a | b); break;
                            case Opcode.XOR: result = (a ^ b); break;
                            default:
                                             {
                                                 SetState(ExecutionState.Fault);
                                                 return;
                                             }
                        }

                        frame.Registers[dst].SetValue(result);
                        break;

                    }

                default:
                    throw new VMException(frame.VM, "logical op unsupported for type " + valA.Type);
            }

        }

        /// <summary>
        /// Opcode - EQUAL -> Operation Equal
        /// args -> byte src_a_reg, byte src_b_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Equal(ref ExecutionFrame frame)
        {
            var srcA = Read8();
            var srcB = Read8();
            var dst = Read8();

            Expect(srcA < frame.Registers.Length, "invalid srcA register");
            Expect(srcB < frame.Registers.Length, "invalid srcB register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var a = frame.Registers[srcA];
            var b = frame.Registers[srcB];

            var result = a.Equals(b);
            frame.Registers[dst].SetValue(result);
        }

        /// <summary>
        /// Opcode - LT -> Operation Less Than
        /// Opcode - GT -> Operation Greater Than
        /// Opcode - LTE -> Operation Less Than Equals
        /// Opcode - GTE -> Operation Greater Than Equals
        /// args -> byte src_a_reg, byte src_b_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Comparison(ref ExecutionFrame frame)
        {
            var srcA = Read8();
            var srcB = Read8();
            var dst = Read8();

            Expect(srcA < frame.Registers.Length, "invalid srcA register");
            Expect(srcB < frame.Registers.Length, "invalid srcB register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var a = frame.Registers[srcA].AsNumber();
            var b = frame.Registers[srcB].AsNumber();

            bool result;
            switch (opcode)
            {
                case Opcode.LT: result = (a < b); break;
                case Opcode.GT: result = (a > b); break;
                case Opcode.LTE: result = (a <= b); break;
                case Opcode.GTE: result = (a >= b); break;
                default:
                                 {
                                     SetState(ExecutionState.Fault);
                                     return;
                                 }
            }

            frame.Registers[dst].SetValue(result);
        }

        /// <summary>
        /// Opcode - INC -> Operation Increment
        /// args -> byte reg
        /// </summary>
        /// <param name="frame"></param>
        private void Increment(ref ExecutionFrame frame)
        {
            var dst = Read8();
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[dst].AsNumber();
            frame.Registers[dst].SetValue(val + 1);
        }

        /// <summary>
        /// Opcode - DEC -> Operation Decrement
        /// args -> byte reg
        /// </summary>
        /// <param name="frame"></param>
        private void Decrement(ref ExecutionFrame frame)
        {
            var dst = Read8();
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[dst].AsNumber();
            frame.Registers[dst].SetValue(val - 1);

        }

        /// <summary>
        /// Opcode - SIGN -> Operation Sign
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Sign(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[src].AsNumber();

            if (val == 0)
            {
                frame.Registers[dst].SetValue(BigInteger.Zero);
            }
            else
            {
                frame.Registers[dst].SetValue(val < 0 ? -1 : 1);
            }
        }

        /// <summary>
        /// Opcode - NEGATE -> Operation Negate
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Negate(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            Expect(frame.Registers[src].Type is VMType.Number or VMType.String or VMType.Timestamp or VMType.Bytes, $"Invalid VM Register type: {frame.Registers[src].Type}");

            var val = frame.Registers[src].AsNumber();
            frame.Registers[dst].SetValue(-val);
        }

        /// <summary>
        /// Opcode - ABS -> Operation Abs
        /// args -> byte src_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void ABS(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");

            var val = frame.Registers[src].AsNumber();
            frame.Registers[dst].SetValue(val < 0 ? -val : val);
        }


        /// <summary>
        /// Opcode - ADD -> Operation Addition
        /// Opcode - SUB -> Operation Subtraction
        /// Opcode - MUL -> Operation Multiplication
        /// Opcode - DIV -> Operation Division
        /// Opcode - MOD -> Operation MOD
        /// Opcode - SHR -> Operation SHR
        /// Opcode - SHL -> Operation SHL
        /// Opcode - MIN -> Operation Minimum
        /// Opcode - MAX -> Operation Maximum
        /// Opcode - POW -> Operation Power
        /// args -> byte src_a_reg, byte src_b_reg, byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Operations(ref ExecutionFrame frame)
        {
            var srcA = Read8();
            var srcB = Read8();
            var dst = Read8();

            Expect(srcA < frame.Registers.Length, "invalid srcA register");
            Expect(srcB < frame.Registers.Length, "invalid srcB register");
            Expect(dst < frame.Registers.Length, "invalid dst register");


            if (opcode == Opcode.ADD && frame.Registers[srcA].Type == VMType.String)
            {
                Expect(frame.Registers[srcB].Type == VMType.String, "invalid string as right operand");

                var a = frame.Registers[srcA].AsString();
                var b = frame.Registers[srcB].AsString();

                var result = a + b;
                frame.Registers[dst].SetValue(result);
            }
            else
            {
                var a = frame.Registers[srcA].AsNumber();
                var b = frame.Registers[srcB].AsNumber();

                BigInteger result;

                switch (opcode)
                {
                    case Opcode.ADD: result = a + b; break;
                    case Opcode.SUB: result = a - b; break;
                    case Opcode.MUL: result = a * b; break;
                    case Opcode.DIV: result = a / b; break;
                    case Opcode.MOD: result = a % b; break;
                    case Opcode.SHR: result = a >> (int)b; break;
                    case Opcode.SHL: result = a << (int)b; break;
                    case Opcode.MIN: result = a < b ? a : b; break;
                    case Opcode.MAX: result = a > b ? a : b; break;
                    case Opcode.POW: result = BigInteger.Pow(a, (int)b); break;
                    default:
                                     {
                                         SetState(ExecutionState.Fault);
                                         return;
                                     }
                }

                frame.Registers[dst].SetValue(result);
            }

        }

        /// <summary>
        /// Opcode - PUT -> Operation Put
        /// args -> byte src_reg, byte dest_reg, byte key
        /// </summary>
        /// <param name="frame"></param>
        private void Put(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var keyReg = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");
            Expect(keyReg < frame.Registers.Length, "invalid key register");

            var key = frame.Registers[keyReg];
            Throw.If(key.Type == VMType.None, "invalid key type");

            var value = frame.Registers[src];

            frame.Registers[dst].SetKey(key, value);
        }

        /// <summary>
        /// Opcode - Get -> Operation Get
        /// args -> byte src_reg, byte dest_reg, byte key
        /// </summary>
        /// <param name="frame"></param>
        private void Get(ref ExecutionFrame frame)
        {
            var src = Read8();
            var dst = Read8();
            var keyReg = Read8();

            Expect(src < frame.Registers.Length, "invalid src register");
            Expect(dst < frame.Registers.Length, "invalid dst register");
            Expect(keyReg < frame.Registers.Length, "invalid key register");

            var key = frame.Registers[keyReg];
            Throw.If(key.Type == VMType.None, "invalid key type");

            var val = frame.Registers[src].GetKey(key);

            frame.Registers[dst] = val;
        }

        /// <summary>
        /// Opcode - Clear -> Operation Clear
        /// args -> byte dest_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Clear(ref ExecutionFrame frame)
        {
            var dst = Read8();

            Expect(dst < frame.Registers.Length, "invalid dst register");

            frame.Registers[dst] = new VMObject();

        }

        /// <summary>
        /// Opcode - Context -> Operation Context
        /// args -> byte dest_reg, var key
        /// </summary>
        /// <param name="frame"></param>
        private void Context(ref ExecutionFrame frame)
        {
            using (var m = new ProfileMarker("CTX"))
            {
                var src = Read8();
                var dst = Read8();

                Expect(src < frame.Registers.Length, "invalid src register");
                Expect(dst < frame.Registers.Length, "invalid dst register");

                var contextName = frame.Registers[src].AsString();

                ExecutionContext context = frame.VM.FindContext(contextName);

                if (context == null)
                {
                    throw new VMException(frame.VM, $"VM ctx instruction failed: could not find context with name '{contextName}'");
                }

                frame.Registers[dst].SetValue(context);
            }
        }

        /// <summary>
        /// Opcode - Switch -> Operation Switch
        /// args -> byte src_reg
        /// </summary>
        /// <param name="frame"></param>
        private void OPSwitch(ref ExecutionFrame frame)
        {
            using (var m = new ProfileMarker("SWITCH"))
            {
                var src = Read8();
                Expect(src < frame.Registers.Length, "invalid src register");

                var context = frame.Registers[src].AsInterop<ExecutionContext>();

                _state = frame.VM.SwitchContext(context, InstructionPointer);

                if (_state == ExecutionState.Halt)
                {
                    _state = ExecutionState.Running;
                    frame.VM.PopFrame();
                }
                else
                {
                    throw new VMException(frame.VM, $"VM switch instruction failed: execution state did not halt");
                }
            }
        }

        /// <summary>
        /// Opcode - Unpack -> Operation Unpack
        /// args -> byte src_reg dst_reg
        /// </summary>
        /// <param name="frame"></param>
        private void Unpack(ref ExecutionFrame frame)
        {
            using (var m = new ProfileMarker("SWITCH"))
            {
                var src = Read8();
                var dst = Read8();

                Expect(src < frame.Registers.Length, "invalid src register");
                Expect(dst < frame.Registers.Length, "invalid dst register");

                var bytes = frame.Registers[src].AsByteArray();
                frame.Registers[dst] = VMObject.FromBytes(bytes);
            }
        }

#endregion

    }
}
