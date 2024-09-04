using System.Collections.Generic;
using UnityEditor.Build.Content;

public class NesCpu
{
    public byte a = 0x00; // accumulator register
    public byte x = 0x00; // X Register
    public byte y = 0x00; // Y Register
    public byte stkp = 0x00; // Stack Pointer
    public ushort pc = 0x0000; // counter
    byte status = 0x00; // Status Register

	// The status register stores 8 flags. Ive enumerated these here for ease
	// of access. You can access the status register directly since its public.
	// The bits have different interpretations depending upon the context and 
	// instruction being executed.
	public enum FLAGS6502
	{
		C = (1 << 0),	// Carry Bit
		Z = (1 << 1),	// Zero
		I = (1 << 2),	// Disable Interrupts
		D = (1 << 3),	// Decimal Mode (unused in this implementation)
		B = (1 << 4),	// Break
		U = (1 << 5),	// Unused
		V = (1 << 6),	// Overflow
		N = (1 << 7),	// Negative
	};

	// Convenience functions to access status register
	public byte GetFlag(FLAGS6502 f)
    {
        return (byte)((status & (byte)f) > 0 ? 1 : 0);
    }
	void SetFlag(FLAGS6502 f, bool v)
    {
        if (v) {
            status |= (byte)f;
        } else {
            status &= (byte)~f;
        }
    }

	void SetFlag(FLAGS6502 f, int v)
    {
        if (v > 0) {
            status |= (byte)f;
        } else {
            status &= (byte)~f;
        }
    }

	// Assisstive variables to facilitate emulation
	byte  fetched     = 0x00;   // Represents the working input value to the ALU
	ushort temp        = 0x0000; // A convenience variable used everywhere
	ushort addr_abs    = 0x0000; // All used memory addresses end up in here
	ushort addr_rel    = 0x0000;   // Represents absolute address following a branch
	byte  opcode      = 0x00;   // Is the instruction byte
	byte  cycles      = 0;	   // Counts how many cycles the instruction has remaining
	uint clock_count = 0;	   // A global accumulation of the number of clocks

    delegate byte operate_funptr();
    delegate byte addrmode_funptr();
    struct Instruction
    {
        public string name;
        public operate_funptr operate;
        public addrmode_funptr addrmode;
        public byte cycles;
    }
    List<Instruction> opLookup;

    NesBus bus;

    public NesCpu()
    {
        opLookup = new List<Instruction>() {
            new Instruction{ name = "BRK", operate = BRK, addrmode = IMM, cycles = 7 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "ASL", operate = ASL, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "PHP", operate = PHP, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "ASL", operate = ASL, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "ASL", operate = ASL, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BPL", operate = BPL, addrmode = REL, cycles = 2 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "ASL", operate = ASL, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "CLC", operate = CLC, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ORA", operate = ORA, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "ASL", operate = ASL, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "JSR", operate = JSR, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "AND", operate = AND, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "BIT", operate = BIT, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "AND", operate = AND, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "ROL", operate = ROL, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "PLP", operate = PLP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "AND", operate = AND, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "ROL", operate = ROL, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "BIT", operate = BIT, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "AND", operate = AND, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "ROL", operate = ROL, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BMI", operate = BMI, addrmode = REL, cycles = 2 },
            new Instruction{ name = "AND", operate = AND, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "AND", operate = AND, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "ROL", operate = ROL, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "SEC", operate = SEC, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "AND", operate = AND, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "AND", operate = AND, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "ROL", operate = ROL, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "RTI", operate = RTI, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "LSR", operate = LSR, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "PHA", operate = PHA, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "LSR", operate = LSR, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "JMP", operate = JMP, addrmode = ABS, cycles = 3 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "LSR", operate = LSR, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BVC", operate = BVC, addrmode = REL, cycles = 2 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "LSR", operate = LSR, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "CLI", operate = CLI, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "EOR", operate = EOR, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "LSR", operate = LSR, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "RTS", operate = RTS, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "ROR", operate = ROR, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "PLA", operate = PLA, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "ROR", operate = ROR, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "JMP", operate = JMP, addrmode = IND, cycles = 5 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "ROR", operate = ROR, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BVS", operate = BVS, addrmode = REL, cycles = 2 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "ROR", operate = ROR, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "SEI", operate = SEI, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "ADC", operate = ADC, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "ROR", operate = ROR, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "STA", operate = STA, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "STY", operate = STY, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "STA", operate = STA, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "STX", operate = STX, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "DEY", operate = DEY, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "TXA", operate = TXA, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "STY", operate = STY, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "STA", operate = STA, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "STX", operate = STX, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "BCC", operate = BCC, addrmode = REL, cycles = 2 },
            new Instruction{ name = "STA", operate = STA, addrmode = IZY, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "STY", operate = STY, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "STA", operate = STA, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "STX", operate = STX, addrmode = ZPY, cycles = 4 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "TYA", operate = TYA, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "STA", operate = STA, addrmode = ABY, cycles = 5 },
            new Instruction{ name = "TXS", operate = TXS, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "STA", operate = STA, addrmode = ABX, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "LDY", operate = LDY, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "LDX", operate = LDX, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "LDY", operate = LDY, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "LDX", operate = LDX, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 3 },
            new Instruction{ name = "TAY", operate = TAY, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "TAX", operate = TAX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "LDY", operate = LDY, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "LDX", operate = LDX, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "BCS", operate = BCS, addrmode = REL, cycles = 2 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "LDY", operate = LDY, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "LDX", operate = LDX, addrmode = ZPY, cycles = 4 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "CLV", operate = CLV, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "TSX", operate = TSX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "LDY", operate = LDY, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "LDA", operate = LDA, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "LDX", operate = LDX, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "CPY", operate = CPY, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "CPY", operate = CPY, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "DEC", operate = DEC, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "INY", operate = INY, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "DEX", operate = DEX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "CPY", operate = CPY, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "DEC", operate = DEC, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BNE", operate = BNE, addrmode = REL, cycles = 2 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "DEC", operate = DEC, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "CLD", operate = CLD, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "NOP", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "CMP", operate = CMP, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "DEC", operate = DEC, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "CPX", operate = CPX, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = IZX, cycles = 6 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "CPX", operate = CPX, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = ZP0, cycles = 3 },
            new Instruction{ name = "INC", operate = INC, addrmode = ZP0, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 5 },
            new Instruction{ name = "INX", operate = INX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = IMM, cycles = 2 },
            new Instruction{ name = "NOP", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = SBC, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "CPX", operate = CPX, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = ABS, cycles = 4 },
            new Instruction{ name = "INC", operate = INC, addrmode = ABS, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "BEQ", operate = BEQ, addrmode = REL, cycles = 2 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = IZY, cycles = 5 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 8 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = ZPX, cycles = 4 },
            new Instruction{ name = "INC", operate = INC, addrmode = ZPX, cycles = 6 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 6 },
            new Instruction{ name = "SED", operate = SED, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = ABY, cycles = 4 },
            new Instruction{ name = "NOP", operate = NOP, addrmode = IMP, cycles = 2 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
            new Instruction{ name = "???", operate = NOP, addrmode = IMP, cycles = 4 },
            new Instruction{ name = "SBC", operate = SBC, addrmode = ABX, cycles = 4 },
            new Instruction{ name = "INC", operate = INC, addrmode = ABX, cycles = 7 },
            new Instruction{ name = "???", operate = XXX, addrmode = IMP, cycles = 7 },
        };
    }

    public void ConnectBus(NesBus bus)
    {
        this.bus = bus;
    }

    // This is the disassembly function. Its workings are not required for emulation.
    // It is merely a convenience function to turn the binary instruction code into
    // human readable form. Its included as part of the emulator because it can take
    // advantage of many of the CPUs internal operations to do this.
    public Dictionary<ushort, string> Disassemble(ushort nStart, ushort nStop)
    {
        uint addr = nStart;
        byte value = 0x00, lo = 0x00, hi = 0x00;
        Dictionary<ushort, string> mapLines = new Dictionary<ushort, string>();
        ushort line_addr = 0;

        // A convenient utility to convert variables into
        // hex strings because "modern C++"'s method with 
        // streams is atrocious
        string hex(uint n, byte d)
        {
            string s = new string('0', d);
            var arr = s.ToCharArray();
            for (int i = d - 1; i >= 0; i--, n >>= 4)
                arr[i] = "0123456789ABCDEF"[(int)(n & 0xF)];
            return new string(arr);
        };

        // Starting at the specified address we read an instruction
        // byte, which in turn yields information from the lookup table
        // as to how many additional bytes we need to read and what the
        // addressing mode is. I need this info to assemble human readable
        // syntax, which is different depending upon the addressing mode

        // As the instruction is decoded, a std::string is assembled
        // with the readable output
        while (addr <= nStop)
        {
            line_addr = (ushort)addr;

            // Prefix line with instruction address
            string sInst = "$" + hex(addr, 4) + ": ";

            // Read instruction, and get its readable name
            byte opcode = read((ushort)addr);
            addr++;
            sInst += opLookup[opcode].name + " ";

            // Get oprands from desired locations, and form the
            // instruction based upon its addressing mode. These
            // routines mimmick the actual fetch routine of the
            // 6502 in order to get accurate data as part of the
            // instruction
            if (opLookup[opcode].addrmode == IMP)
            {
                sInst += " {IMP}";
            }
            else if (opLookup[opcode].addrmode == IMM)
            {
                value = read(addr); addr++;
                sInst += "#$" + hex(value, 2) + " {IMM}";
            }
            else if (opLookup[opcode].addrmode == ZP0)
            {
                lo = read(addr); addr++;
                hi = 0x00;												
                sInst += "$" + hex(lo, 2) + " {ZP0}";
            }
            else if (opLookup[opcode].addrmode == ZPX)
            {
                lo = read(addr); addr++;
                hi = 0x00;														
                sInst += "$" + hex(lo, 2) + ", X {ZPX}";
            }
            else if (opLookup[opcode].addrmode == ZPY)
            {
                lo = read(addr); addr++;
                hi = 0x00;														
                sInst += "$" + hex(lo, 2) + ", Y {ZPY}";
            }
            else if (opLookup[opcode].addrmode == IZX)
            {
                lo = read(addr); addr++;
                hi = 0x00;								
                sInst += "($" + hex(lo, 2) + ", X) {IZX}";
            }
            else if (opLookup[opcode].addrmode == IZY)
            {
                lo = read(addr); addr++;
                hi = 0x00;								
                sInst += "($" + hex(lo, 2) + "), Y {IZY}";
            }
            else if (opLookup[opcode].addrmode == ABS)
            {
                lo = read(addr); addr++;
                hi = read(addr); addr++;
                sInst += "$" + hex((uint)((hi << 8) | lo), 4) + " {ABS}";
            }
            else if (opLookup[opcode].addrmode == ABX)
            {
                lo = read(addr); addr++;
                hi = read(addr); addr++;
                sInst += "$" + hex((uint)(hi << 8) | lo, 4) + ", X {ABX}";
            }
            else if (opLookup[opcode].addrmode == ABY)
            {
                lo = read(addr); addr++;
                hi = read(addr); addr++;
                sInst += "$" + hex((uint)(hi << 8) | lo, 4) + ", Y {ABY}";
            }
            else if (opLookup[opcode].addrmode == IND)
            {
                lo = read(addr); addr++;
                hi = read(addr); addr++;
                sInst += "($" + hex((uint)(hi << 8) | lo, 4) + ") {IND}";
            }
            else if (opLookup[opcode].addrmode == REL)
            {
                value = read(addr); addr++;
                sInst += "$" + hex(value, 2) + " [$" + hex(addr + value, 4) + "] {REL}";
            }

            // Add the formed string to a std::map, using the instruction's
            // address as the key. This makes it convenient to look for later
            // as the instructions are variable in length, so a straight up
            // incremental index is not sufficient.
            mapLines[line_addr] = sInst;
        }

        return mapLines;
    }

    public void Reset()
    {
        // Get address to set program counter to
        addr_abs = 0xFFFC;
        ushort lo = read((ushort)(addr_abs + 0));
        ushort hi = read((ushort)(addr_abs + 1));

        // Set it
        pc = (ushort)((hi << 8) | lo);

        // Reset internal registers
        a = 0;
        x = 0;
        y = 0;
        stkp = 0xFD;
        status = (byte)(0x00 | FLAGS6502.U);

        // Clear internal helper variables
        addr_rel = 0x0000;
        addr_abs = 0x0000;
        fetched = 0x00;

        // Reset takes time
        cycles = 8;
    }


    public byte read(ushort a, bool bReadOnly = false)
    {
        return bus.cpuRead(a, bReadOnly);
    }

    public byte read(uint a)
    {
        return read((ushort)a);
    }

    void write(ushort a, byte d)
    {
        bus.cpuWrite(a,d);
    }

    byte fetch()
    {
        if (!(opLookup[opcode].addrmode == IMP)) {
            fetched = read(addr_abs);
        }
        return fetched;
    }

    void doBranch()
    {
        cycles++;
        addr_abs = (ushort)(pc + addr_rel);
        if ((addr_abs & 0xFF00) != (pc & 0xFF00)) {
            cycles++;
        }
        pc = addr_abs;
    }

    public void clock()
    {
        // Each instruction requires a variable number of clock cycles to execute.
        // In my emulation, I only care about the final result and so I perform
        // the entire computation in one hit. In hardware, each clock cycle would
        // perform "microcode" style transformations of the CPUs state.
        //
        // To remain compliant with connected devices, it's important that the 
        // emulation also takes "time" in order to execute instructions, so I
        // implement that delay by simply counting down the cycles required by 
        // the instruction. When it reaches 0, the instruction is complete, and
        // the next one is ready to be executed.
        if (cycles == 0)
        {
            // Read next instruction byte. This 8-bit value is used to index
            // the translation table to get the relevant information about
            // how to implement the instruction
            opcode = read(pc);

    // #ifdef LOGMODE
    //         uint16_t log_pc = pc;
    // #endif
            
            // Always set the unused status flag bit to 1
            SetFlag(FLAGS6502.U, true);
            
            // Increment program counter, we read the opcode byte
            pc++;

            // Get Starting number of cycles
            cycles = opLookup[opcode].cycles;

            // Perform fetch of intermmediate data using the
            // required addressing mode
            byte additional_cycle1 = opLookup[opcode].addrmode();

            // Perform operation
            byte additional_cycle2 = opLookup[opcode].operate();

            // The addressmode and opcode may have altered the number
            // of cycles this instruction requires before its completed
            cycles += (byte)(additional_cycle1 & additional_cycle2);

            // Always set the unused status flag bit to 1
            SetFlag(FLAGS6502.U, true);

    // #ifdef LOGMODE
    //         // This logger dumps every cycle the entire processor state for analysis.
    //         // This can be used for debugging the emulation, but has little utility
    //         // during emulation. Its also very slow, so only use if you have to.
    //         if (logfile == nullptr)	logfile = fopen("olc6502.txt", "wt");
    //         if (logfile != nullptr)
    //         {
    //             fprintf(logfile, "%10d:%02d PC:%04X %s A:%02X X:%02X Y:%02X %s%s%s%s%s%s%s%s STKP:%02X\n",
    //                 clock_count, 0, log_pc, "XXX", a, x, y,	
    //                 GetFlag(N) ? "N" : ".",	GetFlag(V) ? "V" : ".",	GetFlag(U) ? "U" : ".",	
    //                 GetFlag(B) ? "B" : ".",	GetFlag(D) ? "D" : ".",	GetFlag(I) ? "I" : ".",	
    //                 GetFlag(Z) ? "Z" : ".",	GetFlag(C) ? "C" : ".",	stkp);
    //         }
    // #endif
        }
        
        // Increment global clock count - This is actually unused unless logging is enabled
        // but I've kept it in because its a handy watch variable for debugging
        clock_count++;

        // Decrement the number of cycles remaining for this instruction
        cycles--;
    }

    public bool IsComplete()
    {
        return cycles == 0;
    }

#region address modes & opcodes
	// Addressing Modes =============================================
	// The 6502 has a variety of addressing modes to access data in 
	// memory, some of which are direct and some are indirect (like
	// pointers in C++). Each opcode contains information about which
	// addressing mode should be employed to facilitate the 
	// instruction, in regards to where it reads/writes the data it
	// uses. The address mode changes the number of bytes that
	// makes up the full instruction, so we implement addressing
	// before executing the instruction, to make sure the program
	// counter is at the correct location, the instruction is
	// primed with the addresses it needs, and the number of clock
	// cycles the instruction requires is calculated. These functions
	// may adjust the number of cycles required depending upon where
	// and how the memory is accessed, so they return the required
	// adjustment.

    // Address Mode: Implied
    // There is no additional data required for this instruction. The instruction
    // does something very simple like like sets a status bit. However, we will
    // target the accumulator, for instructions like PHA
	byte IMP()
    {
        fetched = a;
        return 0;
    }

    // Address Mode: Immediate
    // The instruction expects the next byte to be used as a value, so we'll prep
    // the read address to point to the next byte    
    byte IMM()
    {
        addr_abs = pc++;
        return 0;
    }

    // Address Mode: Zero Page
    // To save program bytes, zero page addressing allows you to absolutely address
    // a location in first 0xFF bytes of address range. Clearly this only requires
    // one byte instead of the usual two.
	byte ZP0()
    {
        addr_abs = read(pc);
        pc++;
        addr_abs &= 0x00FF;
        return 0;
    }

    // Address Mode: Zero Page with X Offset
    // Fundamentally the same as Zero Page addressing, but the contents of the X Register
    // is added to the supplied single byte address. This is useful for iterating through
    // ranges within the first page.
    byte ZPX()
    {
        addr_abs = ((ushort)(read(pc) + x));
        pc++;
        addr_abs &= 0x00FF;
        return 0;
    }

    // Address Mode: Zero Page with Y Offset
    // Same as above but uses Y Register for offset
	byte ZPY()
    {
        addr_abs = ((ushort)(read(pc) + y));
        pc++;
        addr_abs &= 0x00FF;
        return 0;
    }

    // Address Mode: Relative
    // This address mode is exclusive to branch instructions. The address
    // must reside within -128 to +127 of the branch instruction, i.e.
    // you cant directly branch to any address in the addressable range.    
    byte REL()
    {
        addr_rel = read(pc);
        pc++;
        if ((ushort)(addr_rel & 0x80) > 0) {
            addr_rel |= 0xFF00;
        }
        return 0;
    }

    // Address Mode: Absolute 
    // A full 16-bit address is loaded and used
	byte ABS()
    {
        ushort lo = read(pc++);
        ushort hi = read(pc++);
        addr_abs = (ushort)((hi << 8) | lo);
        return 0;
    }

    // Address Mode: Absolute with X Offset
    // Fundamentally the same as absolute addressing, but the contents of the X Register
    // is added to the supplied two byte address. If the resulting address changes
    // the page, an additional clock cycle is required    
    byte ABX()
    {
        ushort lo = read(pc++);
        ushort hi = read(pc++);
        addr_abs = (ushort)((hi << 8) | lo);
        addr_abs += x;

        if ((addr_abs & 0xFF00) != (hi << 8)) {
            return 1;
        }

        return 0;
    }

    // Address Mode: Absolute with Y Offset
    // Fundamentally the same as absolute addressing, but the contents of the Y Register
    // is added to the supplied two byte address. If the resulting address changes
    // the page, an additional clock cycle is required    
	byte ABY()
    {
        ushort lo = read(pc++);
        ushort hi = read(pc++);
        addr_abs = (ushort)((hi << 8) | lo);
        addr_abs += y;

        if ((addr_abs & 0xFF00) != (hi << 8)) {
            return 1;
        }

        return 0;
    }

    // Note: The next 3 address modes use indirection (aka Pointers!)

    // Address Mode: Indirect
    // The supplied 16-bit address is read to get the actual 16-bit address. This is
    // instruction is unusual in that it has a bug in the hardware! To emulate its
    // function accurately, we also need to emulate this bug. If the low byte of the
    // supplied address is 0xFF, then to read the high byte of the actual address
    // we need to cross a page boundary. This doesnt actually work on the chip as 
    // designed, instead it wraps back around in the same page, yielding an 
    // invalid actual address    
    byte IND()
    {
        ushort ptr_lo = read(pc++);
        ushort ptr_hi = read(pc++);
        ushort ptr = (ushort)((ptr_hi << 8) | ptr_lo);

        if (ptr_lo == 0x00FF) //Simulate page boundary hardware bug
        {
            addr_abs = (ushort)(read((ushort)(ptr & 0xFF00)) << 8 | read(ptr));
        }
        else {
            addr_abs = (ushort)(read((ushort)(ptr + 1)) << 8 | read(ptr));
        }

        return 0;
    }

    // Address Mode: Indirect X
    // The supplied 8-bit address is offset by X Register to index
    // a location in page 0x00. The actual 16-bit address is read 
    // from this location
	byte IZX()
    {
        ushort t = read(pc++);
        ushort lo = read((ushort)((ushort)(t + x) & 0x00FF));
        ushort hi = read((ushort)((ushort)(t + x + 1) & 0x00FF));
        addr_abs = (ushort)((hi << 8) | lo);
        return 0;
    }

    // Address Mode: Indirect Y
    // The supplied 8-bit address indexes a location in page 0x00. From 
    // here the actual 16-bit address is read, and the contents of
    // Y Register is added to it to offset it. If the offset causes a
    // change in page then an additional clock cycle is required.    
    byte IZY()
    {
        ushort t = read(pc++);
        ushort lo = read((ushort)(t & 0x00FF));
        ushort hi = read((ushort)((t + 1) & 0x00FF));
        addr_abs = (ushort)((hi << 8) | lo);
        addr_abs += y;
        if ((addr_abs & 0xFF00) != (hi << 8)) {
            return 1;
        }
        return 0;
    }

    // Opcodes ======================================================
	// There are 56 "legitimate" opcodes provided by the 6502 CPU. I
	// have not modelled "unofficial" opcodes. As each opcode is 
	// defined by 1 byte, there are potentially 256 possible codes.
	// Codes are not used in a "switch case" style on a processor,
	// instead they are repsonisble for switching individual parts of
	// CPU circuits on and off. The opcodes listed here are official, 
	// meaning that the functionality of the chip when provided with
	// these codes is as the developers intended it to be. Unofficial
	// codes will of course also influence the CPU circuitry in 
	// interesting ways, and can be exploited to gain additional
	// functionality!
	//
	// These functions return 0 normally, but some are capable of
	// requiring more clock cycles when executed under certain
	// conditions combined with certain addressing modes. If that is 
	// the case, they return 1.
	//
	// I have included detailed explanations of each function in 
	// the class implementation file. Note they are listed in
	// alphabetical order here for ease of finding.

    ///////////////////////////////////////////////////////////////////////////////
    // INSTRUCTION IMPLEMENTATIONS

    // Note: Ive started with the two most complicated instructions to emulate, which
    // ironically is addition and subtraction! Ive tried to include a detailed 
    // explanation as to why they are so complex, yet so fundamental. Im also NOT
    // going to do this through the explanation of 1 and 2's complement.

    // Instruction: Add with Carry In
    // Function:    A = A + M + C
    // Flags Out:   C, V, N, Z
    //
    // Explanation:
    // The purpose of this function is to add a value to the accumulator and a carry bit. If
    // the result is > 255 there is an overflow setting the carry bit. Ths allows you to
    // chain together ADC instructions to add numbers larger than 8-bits. This in itself is
    // simple, however the 6502 supports the concepts of Negativity/Positivity and Signed Overflow.
    //
    // 10000100 = 128 + 4 = 132 in normal circumstances, we know this as unsigned and it allows
    // us to represent numbers between 0 and 255 (given 8 bits). The 6502 can also interpret 
    // this word as something else if we assume those 8 bits represent the range -128 to +127,
    // i.e. it has become signed.
    //
    // Since 132 > 127, it effectively wraps around, through -128, to -124. This wraparound is
    // called overflow, and this is a useful to know as it indicates that the calculation has
    // gone outside the permissable range, and therefore no longer makes numeric sense.
    //
    // Note the implementation of ADD is the same in binary, this is just about how the numbers
    // are represented, so the word 10000100 can be both -124 and 132 depending upon the 
    // context the programming is using it in. We can prove this!
    //
    //  10000100 =  132  or  -124
    // +00010001 = + 17      + 17
    //  ========    ===       ===     See, both are valid additions, but our interpretation of
    //  10010101 =  149  or  -107     the context changes the value, not the hardware!
    //
    // In principle under the -128 to 127 range:
    // 10000000 = -128, 11111111 = -1, 00000000 = 0, 00000000 = +1, 01111111 = +127
    // therefore negative numbers have the most significant set, positive numbers do not
    //
    // To assist us, the 6502 can set the overflow flag, if the result of the addition has
    // wrapped around. V <- ~(A^M) & A^(A+M+C) :D lol, let's work out why!
    //
    // Let's suppose we have A = 30, M = 10 and C = 0
    //          A = 30 = 00011110
    //          M = 10 = 00001010+
    //     RESULT = 40 = 00101000
    //
    // Here we have not gone out of range. The resulting significant bit has not changed.
    // So let's make a truth table to understand when overflow has occurred. Here I take
    // the MSB of each component, where R is RESULT.
    //
    // A  M  R | V | A^R | A^M |~(A^M) | 
    // 0  0  0 | 0 |  0  |  0  |   1   |
    // 0  0  1 | 1 |  1  |  0  |   1   |
    // 0  1  0 | 0 |  0  |  1  |   0   |
    // 0  1  1 | 0 |  1  |  1  |   0   |  so V = ~(A^M) & (A^R)
    // 1  0  0 | 0 |  1  |  1  |   0   |
    // 1  0  1 | 0 |  0  |  1  |   0   |
    // 1  1  0 | 1 |  1  |  0  |   1   |
    // 1  1  1 | 0 |  0  |  0  |   1   |
    //
    // We can see how the above equation calculates V, based on A, M and R. V was chosen
    // based on the following hypothesis:
    //       Positive Number + Positive Number = Negative Result -> Overflow
    //       Negative Number + Negative Number = Positive Result -> Overflow
    //       Positive Number + Negative Number = Either Result -> Cannot Overflow
    //       Positive Number + Positive Number = Positive Result -> OK! No Overflow
    //       Negative Number + Negative Number = Negative Result -> OK! NO Overflow

	byte ADC()
    {
        fetch();
        temp = (ushort)(a + fetched + GetFlag(FLAGS6502.C));

        SetFlag(FLAGS6502.C, temp > 255);
        SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
        SetFlag(FLAGS6502.V, (((~(a^fetched)) & (a^temp)) & 0x0080) > 0);
        SetFlag(FLAGS6502.N, (temp & 0x80) > 0);
        a = (byte)(temp & 0x00FF);
        return 1;
    }

    // Instruction: Bitwise Logic AND
    // Function:    A = A & M
    // Flags Out:   N, Z   
    byte AND()
    {
        fetch();
        a = (byte)(a & fetched);
        SetFlag(FLAGS6502.Z, a == 0x00);
        SetFlag(FLAGS6502.N, (a & 0x80) > 0);
        return 1;
    }

    // Instruction: Arithmetic Shift Left
    // Function:    A = C <- (A << 1) <- 0
    // Flags Out:   N, Z, C
    byte ASL()
    {
        fetch();
        temp = (ushort)(fetched << 1);
        SetFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
        SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
        SetFlag(FLAGS6502.N, (temp & 0x80) > 0);
        if (opLookup[opcode].addrmode == IMP) {
            a = (byte)(temp & 0x00FF);
        } else {
            write(addr_abs, (byte)(temp & 0x00FF));
        }
        return 0;
    }

    // Instruction: Branch if Carry Clear
    // Function:    if(C == 0) pc = address 
    byte BCC()
    {
        if (GetFlag(FLAGS6502.C) == 0) {
            doBranch();
        }
        return 0;
    }
    
    // Instruction: Branch if Carry Set
    // Function:    if(C == 1) pc = address
	byte BCS()
    {
        if (GetFlag(FLAGS6502.C) == 1) {
            doBranch();
        }
        return 0;
    }
    
    // Instruction: Branch if Equal
    // Function:    if(Z == 1) pc = address    
    byte BEQ()
    {
        if (GetFlag(FLAGS6502.Z) == 1) {
            doBranch();
        }
        return 0;
    }
    byte BIT()
    {
        fetch();
        temp = (ushort)(a & fetched);
        SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
        SetFlag(FLAGS6502.N, (fetched & (1 << 7)) > 0);
        SetFlag(FLAGS6502.V, (fetched & (1 << 6)) > 0);
        return 0;
    }

    // Instruction: Branch if Negative
    // Function:    if(N == 1) pc = address
    byte BMI()
    {
        if (GetFlag(FLAGS6502.N) == 1) {
            doBranch();
        }
        return 0;
    }

    // Instruction: Branch if Not Equal
    // Function:    if(Z == 0) pc = address
	byte BNE()
    {
        if (GetFlag(FLAGS6502.Z) == 0) {
            doBranch();
        }
        return 0;
    }

    // Instruction: Branch if Positive
    // Function:    if(N == 0) pc = address
    byte BPL()
    {
        if (GetFlag(FLAGS6502.N) == 0) {
            doBranch();
        }
        return 0;
    }

    // Instruction: Break
    // Function:    Program Sourced Interrupt
    byte BRK()
    {
        pc++;
        SetFlag(FLAGS6502.I, true);
        write((ushort)(0x0100 + stkp), (byte)(pc >> 8));
        stkp--;
        write((ushort)(0x0100 + stkp), (byte)pc);
        stkp--;

        SetFlag(FLAGS6502.B, true);
        write((ushort)(0x0100 + stkp), status);
        stkp--;
        SetFlag(FLAGS6502.B, false);

        pc = (ushort)(read(0xFFFE) | (read(0xFFFF) << 8));
        return 0;
    }

    // Instruction: Branch if Overflow Clear
    // Function:    if(V == 0) pc = address
    byte BVC()
    {
        if (GetFlag(FLAGS6502.V) == 0) {
            doBranch();
        }
        return 0;
    }

    // Instruction: Branch if Overflow Set
    // Function:    if(V == 1) pc = address
    byte BVS()
    {
        if (GetFlag(FLAGS6502.V) == 1) {
            doBranch();
        }
        return 0;
    }

    // Instruction: Clear Carry Flag
    // Function:    C = 0
    byte CLC()
    {
        SetFlag(FLAGS6502.C, false);
        return 0;
    }

    // Instruction: Clear Decimal Flag
    // Function:    D = 0
    byte CLD()
    {
        SetFlag(FLAGS6502.D, false);
        return 0;
    }

    // Instruction: Disable Interrupts / Clear Interrupt Flag
    // Function:    I = 0
    byte CLI()
    {
        SetFlag(FLAGS6502.I, false);
        return 0;
    }

    // Instruction: Clear Overflow Flag
    // Function:    V = 0
    byte CLV()
    {
        SetFlag(FLAGS6502.V, false);
        return 0;
    }

    // Instruction: Compare Accumulator
    // Function:    C <- A >= M      Z <- (A - M) == 0
    // Flags Out:   N, C, Z
    byte CMP()
    {
        fetch();
        temp = (ushort)(a - fetched);
        SetFlag(FLAGS6502.C, a >= fetched);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, (temp & 0x0080) > 0);
        return 1;
    }
    
    // Instruction: Compare X Register
    // Function:    C <- X >= M      Z <- (X - M) == 0
    // Flags Out:   N, C, Z
    byte CPX()
    {
        fetch();
        temp = (ushort)(x - fetched);
        SetFlag(FLAGS6502.C, x >= fetched);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        return 0;
    }

    // Instruction: Compare Y Register
    // Function:    C <- Y >= M      Z <- (Y - M) == 0
    // Flags Out:   N, C, Z
    byte CPY()
    {
        fetch();
        temp = (ushort)(y - fetched);
        SetFlag(FLAGS6502.C, y >= fetched);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        return 0;
    }

    // Instruction: Decrement Value at Memory Location
    // Function:    M = M - 1
    // Flags Out:   N, Z
    byte DEC()
    {
        fetch();
        temp = (ushort)(fetched - 1);
        write(addr_abs, (byte)temp);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        return 0;
    }

    // Instruction: Decrement X Register
    // Function:    X = X - 1
    // Flags Out:   N, Z
    byte DEX()
    {
        x--;
        SetFlag(FLAGS6502.Z, x == 0);
        SetFlag(FLAGS6502.N, x & 0x80);
        return 0;
    }

    // Instruction: Decrement Y Register
    // Function:    Y = Y - 1
    // Flags Out:   N, Z    
    byte DEY()
    {
        y--;
        SetFlag(FLAGS6502.Z, y == 0);
        SetFlag(FLAGS6502.N, y & 0x80);
        return 0;
    }

    // Instruction: Bitwise Logic XOR
    // Function:    A = A xor M
    // Flags Out:   N, Z
    byte EOR()
    {
        fetch();
        a = (byte)(a ^ fetched);
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 1;
    }

    // Instruction: Increment Value at Memory Location
    // Function:    M = M + 1
    // Flags Out:   N, Z
    byte INC()
    {
        fetch();
        temp = (ushort)(fetched + 1);
        write(addr_abs, (byte)temp);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        return 0;
    }

    // Instruction: Increment X Register
    // Function:    X = X + 1
    // Flags Out:   N, Z
    byte INX()
    {
        x++;
        SetFlag(FLAGS6502.Z, x == 0);
        SetFlag(FLAGS6502.N, x & 0x80);
        return 0;
    }

    // Instruction: Increment Y Register
    // Function:    Y = Y + 1
    // Flags Out:   N, Z
    byte INY()
    {
        y++;
        SetFlag(FLAGS6502.Z, y == 0);
        SetFlag(FLAGS6502.N, y & 0x80);
        return 0;
    }

    // Instruction: Jump To Location
    // Function:    pc = address
    byte JMP()
    {
        pc = addr_abs;
        return 0;
    }

    // Instruction: Jump To Sub-Routine
    // Function:    Push current pc to stack, pc = address
    byte JSR()
    {
        pc--;
        write((ushort)(0x0100 + stkp), (byte)(pc >> 8));
        stkp--;
        write((ushort)(0x0100 + stkp), (byte)pc);
        stkp--;
        
        pc = addr_abs;
        return 0;
    }

    // Instruction: Load The Accumulator
    // Function:    A = M
    // Flags Out:   N, Z
    byte LDA()
    {
        fetch();
        a = fetched;
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 1;
    }

    // Instruction: Load The X Register
    // Function:    X = M
    // Flags Out:   N, Z
    byte LDX()
    {
        fetch();
        x = fetched;
        SetFlag(FLAGS6502.Z, x == 0);
        SetFlag(FLAGS6502.N, x & 0x80);
        return 1;
    }

    // Instruction: Load The Y Register
    // Function:    Y = M
    // Flags Out:   N, Z
    byte LDY()
    {
        fetch();
        y = fetched;
        SetFlag(FLAGS6502.Z, y == 0);
        SetFlag(FLAGS6502.N, y & 0x80);
        return 1;
    }
    byte LSR()
    {
        fetch();
        SetFlag(FLAGS6502.C, fetched & 0x01);
        temp = (ushort)(fetched >> 1);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        if (opLookup[opcode].addrmode == IMP) {
            a = (byte)temp;
        } else {
            write(addr_abs, (byte)temp);
        }
        return 0;
    }
    byte NOP()
    {
      	// Sadly not all NOPs are equal, Ive added a few here
        // based on https://wiki.nesdev.com/w/index.php/CPU_unofficial_opcodes
        // and will add more based on game compatibility, and ultimately
        // I'd like to cover all illegal opcodes too
        switch (opcode) {
        case 0x1C:
        case 0x3C:
        case 0x5C:
        case 0x7C:
        case 0xDC:
        case 0xFC:
            return 1;
        }
        return 0;
    }

    // Instruction: Bitwise Logic OR
    // Function:    A = A | M
    // Flags Out:   N, Z
    byte ORA()
    {
        fetch();
        a = (byte)(a | fetched);
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 1;
    }

    // Instruction: Push Accumulator to Stack
    // Function:    A -> stack
    byte PHA()
    {
        write((ushort)(0x0100 + stkp), a);
        stkp--;
        return 0;
    }

    // Instruction: Push Status Register to Stack
    // Function:    status -> stack
    // Note:        Break flag is set to 1 before push
    byte PHP()
    {
        write((ushort)(0x0100 + stkp), (byte)(status | (byte)FLAGS6502.B | (byte)FLAGS6502.U));
        SetFlag(FLAGS6502.B, false);
        SetFlag(FLAGS6502.U, false);
        stkp--;
        return 0;
    }

    // Instruction: Pop Accumulator off Stack
    // Function:    A <- stack
    // Flags Out:   N, Z
    byte PLA()
    {
        stkp++;
        a = read((ushort)(0x0100 + stkp));
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 0;
    }

    // Instruction: Pop Status Register off Stack
    // Function:    Status <- stack    
    byte PLP()
    {
        stkp++;
        status = read((ushort)(0x0100 + stkp));
        SetFlag(FLAGS6502.U, true);
        return 0;
    }
    byte ROL()
    {
        fetch();
        temp = (ushort)((fetched << 1) | GetFlag(FLAGS6502.C));
        SetFlag(FLAGS6502.C, temp & 0xFF00);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        if (opLookup[opcode].addrmode == IMP) {
            a = (byte)temp;
        } else {
            write(addr_abs, (byte)temp);
        }
        return 0;
    }
    byte ROR()
    {
        fetch();
        temp = (ushort)((fetched >> 1) | (GetFlag(FLAGS6502.C) << 7));
        SetFlag(FLAGS6502.C, fetched & 0x01);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        if (opLookup[opcode].addrmode == IMP) {
            a = (byte)temp;
        } else {
            write(addr_abs, (byte)temp);
        }
        return 0;
    }
    byte RTI()
    {
        stkp++;
        status = read((ushort)(0x0100 + stkp));
        unchecked {
            status &= (byte)~FLAGS6502.B;
            status &= (byte)~FLAGS6502.U;
        }
        stkp++;
        pc = read((ushort)(0x0100 + stkp));
        stkp++;
        pc |= (ushort)(read((ushort)(0x0100 + stkp)) << 8);
        return 0;
    }
    byte RTS()
    {
        stkp++;
        pc = read((ushort)(0x0100 + stkp));
        stkp++;
        pc |= (ushort)(read((ushort)(0x0100 + stkp)) << 8);
        pc++;
        return 0;
    }

    // Instruction: Subtraction with Borrow In
    // Function:    A = A - M - (1 - C)
    // Flags Out:   C, V, N, Z
    //
    // Explanation:
    // Given the explanation for ADC above, we can reorganise our data
    // to use the same computation for addition, for subtraction by multiplying
    // the data by -1, i.e. make it negative
    //
    // A = A - M - (1 - C)  ->  A = A + -1 * (M - (1 - C))  ->  A = A + (-M + 1 + C)
    //
    // To make a signed positive number negative, we can invert the bits and add 1
    // (OK, I lied, a little bit of 1 and 2s complement :P)
    //
    //  5 = 00000101
    // -5 = 11111010 + 00000001 = 11111011 (or 251 in our 0 to 255 range)
    //
    // The range is actually unimportant, because if I take the value 15, and add 251
    // to it, given we wrap around at 256, the result is 10, so it has effectively 
    // subtracted 5, which was the original intention. (15 + 251) % 256 = 10
    //
    // Note that the equation above used (1-C), but this got converted to + 1 + C.
    // This means we already have the +1, so all we need to do is invert the bits
    // of M, the data(!) therfore we can simply add, exactly the same way we did 
    // before.

    byte SBC()
    {
        fetch();
        ushort value = (ushort)(fetched ^ 0x00FF);
        
        temp = (ushort)(a + value + GetFlag(FLAGS6502.C));
        SetFlag(FLAGS6502.C, temp & 0xFF00);
        SetFlag(FLAGS6502.Z, (byte)temp == 0);
        SetFlag(FLAGS6502.V, (temp ^ a) & (temp ^ value) & 0x0080);
        SetFlag(FLAGS6502.N, temp & 0x0080);
        a = (byte)temp;
        return 1;
    }

    // Instruction: Set Carry Flag
    // Function:    C = 1
    byte SEC()
    {
        SetFlag(FLAGS6502.C, true);
        return 0;
    }

    // Instruction: Set Decimal Flag
    // Function:    D = 1
    byte SED()
    {
        SetFlag(FLAGS6502.D, true);
        return 0;
    }

    // Instruction: Set Interrupt Flag / Enable Interrupts
    // Function:    I = 1
    byte SEI()
    {
        SetFlag(FLAGS6502.I, true);
        return 0;
    }
   
    // Instruction: Store Accumulator at Address
    // Function:    M = A
    byte STA()
    {
        write(addr_abs, a);
        return 0;
    }

    // Instruction: Store X Register at Address
    // Function:    M = X
    byte STX()
    {
        write(addr_abs, x);
        return 0;
    }

    // Instruction: Store Y Register at Address
    // Function:    M = Y
    byte STY()
    {
        write(addr_abs, y);
        return 0;
    }

    // Instruction: Transfer Accumulator to X Register
    // Function:    X = A
    // Flags Out:   N, Z
    byte TAX()
    {
        x = a;
        SetFlag(FLAGS6502.Z, x == 0);
        SetFlag(FLAGS6502.N, x & 0x80);
        return 0;
    }
    byte TAY()
    {
        y = a;
        SetFlag(FLAGS6502.Z, y == 0);
        SetFlag(FLAGS6502.N, y & 0x80);
        return 0;
    }

    // Instruction: Transfer Stack Pointer to X Register
    // Function:    X = stack pointer
    // Flags Out:   N, Z
    byte TSX()
    {
        x = stkp;
        SetFlag(FLAGS6502.Z, x == 0);
        SetFlag(FLAGS6502.N, x & 0x80);
        return 0;
    }

    // Instruction: Transfer X Register to Accumulator
    // Function:    A = X
    // Flags Out:   N, Z
    byte TXA()
    {
        a = x;
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 0;
    }

    
    // Instruction: Transfer X Register to Stack Pointer
    // Function:    stack pointer = X
    byte TXS()
    {
        stkp = x;
        return 0;
    }
    
    // Instruction: Transfer Y Register to Accumulator
    // Function:    A = Y
    // Flags Out:   N, Z
    byte TYA()
    {
        a = y;
        SetFlag(FLAGS6502.Z, a == 0);
        SetFlag(FLAGS6502.N, a & 0x80);
        return 0;
    }
    // I capture all "unofficial" opcodes with this function. It is
    // functionally identical to a NOP
    byte XXX()
    {
        return 0;
    }
#endregion        

    public void nmi()
    {
        write((ushort)(0x0100 + stkp), (byte)((pc >> 8) & 0x00FF));
        stkp--;
        write((ushort)(0x0100 + stkp), (byte)(pc & 0x00FF));
        stkp--;

        SetFlag(FLAGS6502.B, 0);
        SetFlag(FLAGS6502.U, 1);
        SetFlag(FLAGS6502.I, 1);
        write((ushort)(0x0100 + stkp), status);
        stkp--;

        addr_abs = 0xFFFA;
        ushort lo = read((ushort)(addr_abs + 0));
        ushort hi = read((ushort)(addr_abs + 1));
        pc = (ushort)((hi << 8) | lo);

        cycles = 8;
    }
}