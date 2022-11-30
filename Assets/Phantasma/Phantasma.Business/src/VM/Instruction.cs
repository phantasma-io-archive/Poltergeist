using System.Numerics;
using System.Text;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.VM
{
    public struct Instruction
    {
        public uint Offset;
        public Opcode Opcode;
        public object[] Args;

        private static void AppendRegister(StringBuilder sb, object reg)
        {
            sb.Append($" r{(byte)reg}");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Offset.ToString().PadLeft(3, '0'));
            sb.Append(": ");
            sb.Append(Opcode.ToString());

            switch (Opcode)
            {
                case Opcode.MOVE:
                case Opcode.COPY:
                case Opcode.SWAP:
                case Opcode.SIZE:
                case Opcode.COUNT:
                case Opcode.SIGN:
                case Opcode.NOT:
                case Opcode.NEGATE:
                case Opcode.ABS:
                case Opcode.CTX:
                case Opcode.PACK:
                case Opcode.UNPACK:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        break;
                    }

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
                case Opcode.PUT:
                case Opcode.GET:
                case Opcode.AND:
                case Opcode.OR:
                case Opcode.XOR:
                case Opcode.CAT:
                case Opcode.EQUAL:
                case Opcode.LT:
                case Opcode.GT:
                case Opcode.LTE:
                case Opcode.GTE:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        sb.Append(',');
                        AppendRegister(sb, Args[2]);
                        break;
                    }

                case Opcode.LEFT:
                case Opcode.RIGHT:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((ushort)Args[2]);
                        break;
                    }

                case Opcode.CAST:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((int)Args[2]);
                        break;
                    }

                case Opcode.RANGE:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((int)Args[2]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((int)Args[3]);
                        break;
                    }

                case Opcode.CLEAR:
                case Opcode.POP:
                case Opcode.PUSH:
                case Opcode.EXTCALL:
                case Opcode.THROW:
                case Opcode.DEC:
                case Opcode.INC:
                case Opcode.SWITCH:
                    {
                        AppendRegister(sb, Args[0]);
                        break;
                    }

                case Opcode.CALL:
                    {
                        AppendRegister(sb, (byte)Args[0]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((ushort)Args[1]);
                        break;
                    }

                //case Opcode.JMP:
                //case Opcode.JMPIF:
                //case Opcode.JMPNOT:
                //    {

                //    }

                // args: byte dst_reg, byte type, var length, var data_bytes
                case Opcode.LOAD:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        sb.Append(' ');

                        var type = (VMType)Args[1];
                        var bytes = (byte[])Args[2];
                        switch (type)
                        {
                            case VMType.String:
                                sb.Append('"');
                                sb.Append(Encoding.UTF8.GetString(bytes));
                                sb.Append('"');
                                break;

                            case VMType.Number:
                                sb.Append(new BigInteger(bytes));
                                break;

                            default:
                                sb.Append(bytes.Encode());
                                break;
                        }

                        break;
                    }

            }

            return sb.ToString();
        }
    }
}
