public class NesBus
{
    public NesCpu cpu;
    public NesPPU ppu;
    public NesRom rom;
    public byte[] cpuRam = new byte[2048];
    public byte[] controller = new byte[2];

    uint sysClockCounter = 0;
	byte[] controller_state = new byte[2];

	// A simple form of Direct Memory Access is used to swiftly
	// transfer data from CPU bus memory into the OAM memory. It would
	// take too long to sensibly do this manually using a CPU loop, so
	// the program prepares a page of memory with the sprite info required
	// for the next frame and initiates a DMA transfer. This suspends the
	// CPU momentarily while the PPU gets sent data at PPU clock speeds.
	// Note here, that dma_page and dma_addr form a 16-bit address in 
	// the CPU bus address space
	byte dma_page = 0x00;
	byte dma_addr = 0x00;
	byte dma_data = 0x00;

	// DMA transfers need to be timed accurately. In principle it takes
	// 512 cycles to read and write the 256 bytes of the OAM memory, a
	// read followed by a write. However, the CPU needs to be on an "even"
	// clock cycle, so a dummy cycle of idleness may be required
	bool dma_dummy = true;

	// Finally a flag to indicate that a DMA transfer is happening
	bool dma_transfer = false;

    public NesBus()
    {
        cpu = new NesCpu();
        ppu = new NesPPU();
        cpu.ConnectBus(this);
    }

	public void cpuWrite(ushort addr, byte data)
    {
        if (rom.cpuWrite(addr, data))
        {
            // The cartridge "sees all" and has the facility to veto
            // the propagation of the bus transaction if it requires.
            // This allows the cartridge to map any address to some
            // other data, including the facility to divert transactions
            // with other physical devices. The NES does not do this
            // but I figured it might be quite a flexible way of adding
            // "custom" hardware to the NES in the future!
        }
        else if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            // System RAM Address Range. The range covers 8KB, though
            // there is only 2KB available. That 2KB is "mirrored"
            // through this address range. Using bitwise AND to mask
            // the bottom 11 bits is the same as addr % 2048.
            cpuRam[addr & 0x07FF] = data;
        }
        else if (addr >= 0x2000 && addr <= 0x3FFF)
        {
            // PPU Address range. The PPU only has 8 primary registers
            // and these are repeated throughout this range. We can
            // use bitwise AND operation to mask the bottom 3 bits, 
            // which is the equivalent of addr % 8.
            ppu.cpuWrite((ushort)(addr & 0x0007), data);
        }
      	else if (addr == 0x4014)
        {
            // A write to this address initiates a DMA transfer
            dma_page = data;
            dma_addr = 0x00;
            dma_transfer = true;						
        }
      	else if (addr >= 0x4016 && addr <= 0x4017)
        {
            controller_state[addr & 0x0001] = controller[addr & 0x0001];
        }
    }
	public byte cpuRead(ushort addr, bool bReadOnly = false)
    {
        byte data = 0x00;
        if (rom.cpuRead(addr, ref data))
        {
            // Cartridge Address Range
        }
        else if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            // System RAM Address Range, mirrored every 2048
            data = cpuRam[addr & 0x07FF];
        }
        else if (addr >= 0x2000 && addr <= 0x3FFF)
        {
            // PPU Address range, mirrored every 8
            data = ppu.cpuRead((ushort)(addr & 0x0007), bReadOnly);
        }
      	else if (addr >= 0x4016 && addr <= 0x4017)
        {
            data = (byte)((controller_state[addr & 0x0001] & 0x80) > 0 ? 1 : 0);
            controller_state[addr & 0x0001] <<= 1;
        }

        return data;
    }

    public bool loadRom(NesRom rom)
    {
        this.rom = rom;
        this.ppu.loadRom(rom);
        return true;
    }

    public void reset()
    {
        cpu.Reset();
        sysClockCounter = 0;
    }
    public void clock()
    {
        // Clocking. The heart and soul of an emulator. The running
        // frequency is controlled by whatever calls this function.
        // So here we "divide" the clock as necessary and call
        // the peripheral devices clock() function at the correct
        // times.

        // The fastest clock frequency the digital system cares
        // about is equivalent to the PPU clock. So the PPU is clocked
        // each time this function is called.
        ppu.clock();

        // The CPU runs 3 times slower than the PPU so we only call its
        // clock() function every 3 times this function is called. We
        // have a global counter to keep track of this.
        if (sysClockCounter % 3 == 0)
        {
            // Is the system performing a DMA transfer form CPU memory to 
            // OAM memory on PPU?...
            if (dma_transfer)
            {
                // ...Yes! We need to wait until the next even CPU clock cycle
                // before it starts...
                if (dma_dummy)
                {
                    // ...So hang around in here each clock until 1 or 2 cycles
                    // have elapsed...
                    if (sysClockCounter % 2 == 1)
                    {
                        // ...and finally allow DMA to start
                        dma_dummy = false;
                    }
                }
                else
                {
                    // DMA can take place!
                    if (sysClockCounter % 2 == 0)
                    {
                        // On even clock cycles, read from CPU bus
                        dma_data = cpuRead((ushort)(dma_page << 8 | dma_addr));
                    }
                    else
                    {
                        // On odd clock cycles, write to PPU OAM
                        ppu.WritePAM(dma_addr, dma_data);
                        // Increment the lo byte of the address
                        dma_addr++;
                        // If this wraps around, we know that 256
                        // bytes have been written, so end the DMA
                        // transfer, and proceed as normal
                        if (dma_addr == 0x00)
                        {
                            dma_transfer = false;
                            dma_dummy = true;
                        }
                    }
                }
            } else {
                cpu.clock();
            }
        }

        // The PPU is capable of emitting an interrupt to indicate the
        // vertical blanking period has been entered. If it has, we need
        // to send that irq to the CPU.
        if (ppu.nmi)
        {
            ppu.nmi = false;
            cpu.nmi();
        }


        sysClockCounter++;
    }

    

}