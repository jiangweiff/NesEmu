using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Mathematics;

public interface INesRomAddrMapper
{
 	bool cpuMapRead(ushort addr, ref uint mapped_addr);
	bool cpuMapWrite(ushort addr, ref uint mapped_addr, byte data);
	// Transform PPU bus address into CHR ROM offset
	bool ppuMapRead(ushort addr, ref uint mapped_addr);
	bool ppuMapWrite(ushort addr, ref uint mapped_addr);
}

public class NesRomAddrMapper000 : INesRomAddrMapper
{
	// These are stored locally as many of the mappers require this information
	byte nPRGBanks = 0;
	byte nCHRBanks = 0;

    public NesRomAddrMapper000(byte prgBanks, byte chrBanks)
    {
        nPRGBanks = prgBanks;
        nCHRBanks = chrBanks;
    }

    public bool cpuMapRead(ushort addr, ref uint mapped_addr)
    {
        // if PRGROM is 16KB
        //     CPU Address Bus          PRG ROM
        //     0x8000 -> 0xBFFF: Map    0x0000 -> 0x3FFF
        //     0xC000 -> 0xFFFF: Mirror 0x0000 -> 0x3FFF
        // if PRGROM is 32KB
        //     CPU Address Bus          PRG ROM
        //     0x8000 -> 0xFFFF: Map    0x0000 -> 0x7FFF	
        if (addr >= 0x8000 && addr <= 0xFFFF)
        {
            mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }

        return false;        
    }

    public bool cpuMapWrite(ushort addr, ref uint mapped_addr, byte data)
    {
        if (addr >= 0x8000 && addr <= 0xFFFF)
        {
            mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }
        return false;
    }

    public bool ppuMapRead(ushort addr, ref uint mapped_addr)
    {
        // There is no mapping required for PPU
        // PPU Address Bus          CHR ROM
        // 0x0000 -> 0x1FFF: Map    0x0000 -> 0x1FFF
        if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            mapped_addr = addr;
            return true;
        }

        return false;
    }

    public bool ppuMapWrite(ushort addr, ref uint mapped_addr)
    {
        if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            if (nCHRBanks == 0)
            {
                // Treat as RAM
                mapped_addr = addr;
                return true;
            }
        }

        return false;
    }
}


public class NesRomAddrMapper002 : INesRomAddrMapper
{
	// These are stored locally as many of the mappers require this information
	byte nPRGBanks = 0;
	byte nCHRBanks = 0;
	byte nPRGBankSelectLo = 0x00;
	byte nPRGBankSelectHi = 0x00;
    public NesRomAddrMapper002(byte prgBanks, byte chrBanks)
    {
        nPRGBanks = prgBanks;
        nCHRBanks = chrBanks;
        nPRGBankSelectLo = 0;
        nPRGBankSelectHi = (byte)(nPRGBanks - 1);
    }

    public bool cpuMapRead(ushort addr, ref uint mapped_addr)
    {
        if (addr >= 0x8000 && addr <= 0xBFFF)
        {
            mapped_addr = (uint)(nPRGBankSelectLo * 0x4000 + (addr & 0x3FFF));
            return true;
        }

        if (addr >= 0xC000 && addr <= 0xFFFF)
        {
            mapped_addr = (uint)(nPRGBankSelectHi * 0x4000 + (addr & 0x3FFF));
            return true;
        }

        return false;        
    }

    public bool cpuMapWrite(ushort addr, ref uint mapped_addr, byte data)
    {
        if (addr >= 0x8000 && addr <= 0xFFFF)
        {
    		nPRGBankSelectLo = (byte)(data & 0x0F);
            return true;
        }
        return false;
    }

    public bool ppuMapRead(ushort addr, ref uint mapped_addr)
    {
       if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            mapped_addr = addr;
            return true;
        }
        return false;
    }

    public bool ppuMapWrite(ushort addr, ref uint mapped_addr)
    {
        if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            if (nCHRBanks == 0)
            {
                // Treat as RAM
                mapped_addr = addr;
                return true;
            }
        }

        return false;
    }
}


public class NesRom
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct RomHeader
    {
        // iNES Format Header
        public fixed byte name[4];
		public byte prg_rom_chunks;
		public byte chr_rom_chunks;
		public byte mapper1;
		public byte mapper2;
		public byte prg_ram_size;
		public byte tv_system1;
		public byte tv_system2;
		public fixed byte unused[5];
    }

   	public enum MIRROR
	{
		HORIZONTAL,
		VERTICAL,
		ONESCREEN_LO,
		ONESCREEN_HI,
	}
    public MIRROR mirror = MIRROR.HORIZONTAL;

   	byte nMapperID = 0;
	byte nPRGBanks = 0;
	byte nCHRBanks = 0;

	byte[] vPRGMemory;
	byte[] vCHRMemory;

    INesRomAddrMapper mapper;

    public void Load(string filename)
    {
        var buffer = File.ReadAllBytes(filename);
        int sHeader = Marshal.SizeOf<RomHeader>();
        var ptr = Marshal.AllocHGlobal(sHeader);
        Marshal.Copy(buffer, 0, ptr, sHeader);
        var header = Marshal.PtrToStructure<RomHeader>(ptr);
        Marshal.FreeHGlobal(ptr);

   		// Determine Mapper ID
		nMapperID = (byte)(((header.mapper2 >> 4) << 4) | (header.mapper1 >> 4));
		mirror = (header.mapper1 & 0x01) > 0 ? MIRROR.VERTICAL : MIRROR.HORIZONTAL;

		// "Discover" File Format
		byte nFileType = 1;
        if ((header.mapper2 & 0x0C) == 0x08) nFileType = 2;

		if (nFileType == 0)
		{
		}

		if (nFileType == 1)
		{
            var cursor = sHeader;
			nPRGBanks = header.prg_rom_chunks;
			vPRGMemory = new byte[nPRGBanks * 16384];
            Array.Copy(buffer, cursor, vPRGMemory, 0, vPRGMemory.Length);
            cursor += vPRGMemory.Length;

			nCHRBanks = header.chr_rom_chunks;
            if (nCHRBanks == 0) {
                vCHRMemory = new byte[8192];
            } else {
                vCHRMemory = new byte[nCHRBanks * 8192];
            }
            Array.Copy(buffer, cursor, vCHRMemory, 0, math.min(buffer.Length-cursor,vCHRMemory.Length));
		}

		if (nFileType == 2)
		{
            var cursor = sHeader;
			nPRGBanks = (byte)(((header.prg_ram_size & 0x07) << 8) | header.prg_rom_chunks);
			vPRGMemory = new byte[nPRGBanks * 16384];
            Array.Copy(buffer, cursor, vPRGMemory, 0, vPRGMemory.Length);
            cursor += vPRGMemory.Length;

			nCHRBanks = (byte)(((header.prg_ram_size & 0x38) << 8) | header.chr_rom_chunks);
			vCHRMemory = new byte[nCHRBanks * 8192];
            Array.Copy(buffer, cursor, vCHRMemory, 0, vCHRMemory.Length);
		}

   		// Load appropriate mapper
		switch (nMapperID)
		{
            case   0: mapper = new NesRomAddrMapper000(nPRGBanks, nCHRBanks); break;
            case   2: mapper = new NesRomAddrMapper002(nPRGBanks, nCHRBanks); break;
            //case   3: pMapper = std::make_shared<Mapper_003>(nPRGBanks, nCHRBanks); break;
            //case  66: pMapper = std::make_shared<Mapper_066>(nPRGBanks, nCHRBanks); break;
		}
    }

    public bool cpuRead(ushort addr, ref byte data)
    {
        uint mapped_addr = 0;
        if (mapper.cpuMapRead(addr, ref mapped_addr))
        {
            data = vPRGMemory[mapped_addr];
            return true;
        }
        else
            return false;
    }

    public bool cpuWrite(ushort addr, byte data)
    {
        uint mapped_addr = 0;
        if (mapper.cpuMapWrite(addr, ref mapped_addr, data))
        {
            vPRGMemory[mapped_addr] = data;
            return true;
        }
        else
            return false;
    }

    public bool ppuRead(ushort addr, ref byte data)
    {
        uint mapped_addr = 0;
        if (mapper.ppuMapRead(addr, ref mapped_addr))
        {
            data = vCHRMemory[mapped_addr];
            return true;
        }
        else
            return false;
    }

    public bool ppuWrite(ushort addr, byte data)
    {
        uint mapped_addr = 0;
        if (mapper.ppuMapRead(addr, ref mapped_addr))
        {
            vCHRMemory[mapped_addr] = data;
            return true;
        }
        else
            return false;
    }

}