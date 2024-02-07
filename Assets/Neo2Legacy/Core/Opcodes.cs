namespace Poltergeist.Neo2.Core
{
    public enum OpCode : byte
    {
        // Constants
        PUSH0 = 0x00, // An empty array of bytes is pushed onto the stack.
        PUSHF = PUSH0,
        PUSHBYTES1 = 0x01, // 0x01-0x4B The next opcode bytes is data to be pushed onto the stack
        PUSHBYTES2 = 0x2,
        PUSHBYTES3 = 0x3,
        PUSHBYTES4 = 0x4,
        PUSHBYTES5 = 0x5,
        PUSHBYTES6 = 0x6,
        PUSHBYTES7 = 0x7,
        PUSHBYTES8 = 0x8,
        PUSHBYTES9 = 0x9,
        PUSHBYTES10 = 0xA,
        PUSHBYTES11 = 0xB,
        PUSHBYTES12 = 0xC,
        PUSHBYTES13 = 0xD,
        PUSHBYTES14 = 0xE,
        PUSHBYTES15 = 0xF,
        PUSHBYTES16 = 0x10,
        PUSHBYTES17 = 0x11,
        PUSHBYTES18 = 0x12,
        PUSHBYTES19 = 0x13,
        PUSHBYTES20 = 0x14,
        PUSHBYTES21 = 0x15,
        PUSHBYTES22 = 0x16,
        PUSHBYTES23 = 0x17,
        PUSHBYTES24 = 0x18,
        PUSHBYTES25 = 0x19,
        PUSHBYTES26 = 0x1A,
        PUSHBYTES27 = 0x1B,
        PUSHBYTES28 = 0x1C,
        PUSHBYTES29 = 0x1D,
        PUSHBYTES30 = 0x1E,
        PUSHBYTES31 = 0x1F,
        PUSHBYTES32 = 0x20,
        PUSHBYTES33 = 0x21,
        PUSHBYTES34 = 0x22,
        PUSHBYTES35 = 0x23,
        PUSHBYTES36 = 0x24,
        PUSHBYTES37 = 0x25,
        PUSHBYTES38 = 0x26,
        PUSHBYTES39 = 0x27,
        PUSHBYTES40 = 0x28,
        PUSHBYTES41 = 0x29,
        PUSHBYTES42 = 0x2A,
        PUSHBYTES43 = 0x2B,
        PUSHBYTES44 = 0x2C,
        PUSHBYTES45 = 0x2D,
        PUSHBYTES46 = 0x2E,
        PUSHBYTES47 = 0x2F,
        PUSHBYTES48 = 0x30,
        PUSHBYTES49 = 0x31,
        PUSHBYTES50 = 0x32,
        PUSHBYTES51 = 0x33,
        PUSHBYTES52 = 0x34,
        PUSHBYTES53 = 0x35,
        PUSHBYTES54 = 0x36,
        PUSHBYTES55 = 0x37,
        PUSHBYTES56 = 0x38,
        PUSHBYTES57 = 0x39,
        PUSHBYTES58 = 0x3A,
        PUSHBYTES59 = 0x3B,
        PUSHBYTES60 = 0x3C,
        PUSHBYTES61 = 0x3D,
        PUSHBYTES62 = 0x3E,
        PUSHBYTES63 = 0x3F,
        PUSHBYTES64 = 0x40,
        PUSHBYTES65 = 0x41,
        PUSHBYTES66 = 0x42,
        PUSHBYTES67 = 0x43,
        PUSHBYTES68 = 0x44,
        PUSHBYTES69 = 0x45,
        PUSHBYTES70 = 0x46,
        PUSHBYTES71 = 0x47,
        PUSHBYTES72 = 0x48,
        PUSHBYTES73 = 0x49,
        PUSHBYTES74 = 0x4A,
        PUSHBYTES75 = 0x4B,
        PUSHDATA1 = 0x0C, // The next byte contains the number of bytes to be pushed onto the stack.
        PUSHDATA2 = 0x0D, // The next two bytes contain the number of bytes to be pushed onto the stack.
        PUSHDATA4 = 0x0E, // The next four bytes contain the number of bytes to be pushed onto the stack.
        PUSHM1 = 0x4F, // The number -1 is pushed onto the stack.
        PUSH1 = 0x51, // The number 1 is pushed onto the stack.
        PUSHT = PUSH1,
        PUSH2 = 0x52, // The number 2 is pushed onto the stack.
        PUSH3 = 0x53, // The number 3 is pushed onto the stack.
        PUSH4 = 0x54, // The number 4 is pushed onto the stack.
        PUSH5 = 0x55, // The number 5 is pushed onto the stack.
        PUSH6 = 0x56, // The number 6 is pushed onto the stack.
        PUSH7 = 0x57, // The number 7 is pushed onto the stack.
        PUSH8 = 0x58, // The number 8 is pushed onto the stack.
        PUSH9 = 0x59, // The number 9 is pushed onto the stack.
        PUSH10 = 0x5A, // The number 10 is pushed onto the stack.
        PUSH11 = 0x5B, // The number 11 is pushed onto the stack.
        PUSH12 = 0x5C, // The number 12 is pushed onto the stack.
        PUSH13 = 0x5D, // The number 13 is pushed onto the stack.
        PUSH14 = 0x5E, // The number 14 is pushed onto the stack.
        PUSH15 = 0x5F, // The number 15 is pushed onto the stack.
        PUSH16 = 0x60, // The number 16 is pushed onto the stack.


        // Flow control
        NOP = 0x61, // Does nothing.
        JMP = 0x62,
        JMPIF = 0x63,
        JMPIFNOT = 0x64,
        CALL = 0x65,
        RET = 0x66,
        APPCALL = 0x67,
        SYSCALL = 0x41,
        TAILCALL = 0x69,


        // Stack
        DUPFROMALTSTACK = 0x6A,
        TOALTSTACK = 0x6B, // Puts the input onto the top of the alt stack. Removes it from the main stack.
        FROMALTSTACK = 0x6C, // Puts the input onto the top of the main stack. Removes it from the alt stack.
        XDROP = 0x6D,
        XSWAP = 0x72,
        XTUCK = 0x73,
        DEPTH = 0x74, // Puts the number of stack items onto the stack.
        DROP = 0x75, // Removes the top stack item.
        DUP = 0x76, // Duplicates the top stack item.
        NIP = 0x77, // Removes the second-to-top stack item.
        OVER = 0x78, // Copies the second-to-top stack item to the top.
        PICK = 0x79, // The item n back in the stack is copied to the top.
        ROLL = 0x7A, // The item n back in the stack is moved to the top.
        ROT = 0x7B, // The top three items on the stack are rotated to the left.
        SWAP = 0x7C, // The top two items on the stack are swapped.
        TUCK = 0x7D, // The item at the top of the stack is copied and inserted before the second-to-top item.


        // Splice
        CAT = 0x7E, // Concatenates two strings.
        SUBSTR = 0x7F, // Returns a section of a string.
        LEFT = 0x80, // Keeps only characters left of the specified point in a string.
        RIGHT = 0x81, // Keeps only characters right of the specified point in a string.
        SIZE = 0x82, // Returns the length of the input string.


        // Bitwise logic
        INVERT = 0x83, // Flips all of the bits in the input.
        AND = 0x84, // Boolean and between each bit in the inputs.
        OR = 0x85, // Boolean or between each bit in the inputs.
        XOR = 0x86, // Boolean exclusive or between each bit in the inputs.
        EQUAL = 0x87, // Returns 1 if the inputs are exactly equal, 0 otherwise.
        //OP_EQUALVERIFY = 0x88, // Same as OP_EQUAL, but runs OP_VERIFY afterward.
        //OP_RESERVED1 = 0x89, // Transaction is invalid unless occuring in an unexecuted OP_IF branch
        //OP_RESERVED2 = 0x8A, // Transaction is invalid unless occuring in an unexecuted OP_IF branch


        // Arithmetic
        // Note: Arithmetic inputs are limited to signed 32-bit integers, but may overflow their output.
        INC = 0x8B, // 1 is added to the input.
        DEC = 0x8C, // 1 is subtracted from the input.
        SIGN = 0x8D,
        NEGATE = 0x8F, // The sign of the input is flipped.
        ABS = 0x90, // The input is made positive.
        NOT = 0x91, // If the input is 0 or 1, it is flipped. Otherwise the output will be 0.
        NZ = 0x92, // Returns 0 if the input is 0. 1 otherwise.
        ADD = 0x93, // a is added to b.
        SUB = 0x94, // b is subtracted from a.
        MUL = 0x95, // a is multiplied by b.
        DIV = 0x96, // a is divided by b.
        MOD = 0x97, // Returns the remainder after dividing a by b.
        SHL = 0x98, // Shifts a left b bits, preserving sign.
        SHR = 0x99, // Shifts a right b bits, preserving sign.
        BOOLAND = 0x9A, // If both a and b are not 0, the output is 1. Otherwise 0.
        BOOLOR = 0x9B, // If a or b is not 0, the output is 1. Otherwise 0.
        NUMEQUAL = 0x9C, // Returns 1 if the numbers are equal, 0 otherwise.
        NUMNOTEQUAL = 0x9E, // Returns 1 if the numbers are not equal, 0 otherwise.
        LT = 0x9F, // Returns 1 if a is less than b, 0 otherwise.
        GT = 0xA0, // Returns 1 if a is greater than b, 0 otherwise.
        LTE = 0xA1, // Returns 1 if a is less than or equal to b, 0 otherwise.
        GTE = 0xA2, // Returns 1 if a is greater than or equal to b, 0 otherwise.
        MIN = 0xA3, // Returns the smaller of a and b.
        MAX = 0xA4, // Returns the larger of a and b.
        WITHIN = 0xA5, // Returns 1 if x is within the specified range (left-inclusive), 0 otherwise.


        // Crypto
        //RIPEMD160 = 0xA6, // The input is hashed using RIPEMD-160.
        SHA1 = 0xA7, // The input is hashed using SHA-1.
        SHA256 = 0xA8, // The input is hashed using SHA-256.
        HASH160 = 0xA9,
        HASH256 = 0xAA,
        CHECKSIG = 0xAC,
        VERIFY = 0xAD,
        CHECKMULTISIG = 0xAE,


        // Array
        ARRAYSIZE = 0xC0,
        PACK = 0xC1,
        UNPACK = 0xC2,
        PICKITEM = 0xC3,
        SETITEM = 0xC4,
        NEWARRAY = 0xC5, //用作引用類型
        NEWSTRUCT = 0xC6, //用作值類型
        NEWMAP = 0xC7,
        APPEND = 0xC8,
        REVERSE = 0xC9,
        REMOVE = 0xCA,
        HASKEY = 0xCB,
        KEYS = 0xCC,
        VALUES = 0xCD,


        // Stack isolation
        CALL_I = 0xE0,
        CALL_E = 0xE1,
        CALL_ED = 0xE2,
        CALL_ET = 0xE3,
        CALL_EDT = 0xE4,


        // Exceptions
        THROW = 0xF0,
        THROWIFNOT = 0xF1
    }
}
