using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class NesPPU
{
    byte[,] tblName = new byte[2,1024];
    byte[,] tblPattern = new byte[2,4096];
    byte[] tblPalette = new byte[32];
    Color32[] palScreen = new Color32[0x40];

    class RegBase
    {
        public byte DumpToByte()
        {
            byte[] data = new byte[8];
            var ptr = Marshal.AllocHGlobal(8);       
            Marshal.StructureToPtr(this, ptr, false);
            Marshal.Copy(ptr, data, 0, 8);
            Marshal.FreeHGlobal(ptr);
            byte val = 0;
            for(int i = 0; i < 8; ++i) {
                val |= (byte)(data[i] > 0 ? 1 << i : 0);
            }
            return val;
        }

        public void CopyFromByte(byte v)
        {
            byte[] data = new byte[8];
            for(int i = 0; i < 8; ++i) {
                data[i] = (byte)(((v & (1 << i)) > 0) ? 1 : 0);
            }

            var ptr = Marshal.AllocHGlobal(8);
            Marshal.Copy(data, 0, ptr, 8);
            Marshal.PtrToStructure(ptr, this);
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    class Status : RegBase
    {
        [FieldOffset(5)] public byte sprite_overflow;
        [FieldOffset(6)] public byte sprite_zero_hit;
        [FieldOffset(7)] public byte vertical_blank;
    }
    Status status = new Status();

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    class Mask : RegBase
    {
        [FieldOffset(0)] public byte grayscale;
		[FieldOffset(1)] public byte render_background_left;
		[FieldOffset(2)] public byte render_sprites_left;
		[FieldOffset(3)] public byte render_background;
		[FieldOffset(4)] public byte render_sprites;
		[FieldOffset(5)] public byte enhance_red;
		[FieldOffset(6)] public byte enhance_green;
		[FieldOffset(7)] public byte enhance_blue;
    }
    Mask mask = new Mask();

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    class PPUCTRL : RegBase
    {
        [FieldOffset(0)] public byte nametable_x;
		[FieldOffset(1)] public byte nametable_y;
		[FieldOffset(2)] public byte increment_mode;
		[FieldOffset(3)] public byte pattern_sprite;
		[FieldOffset(4)] public byte pattern_background;
		[FieldOffset(5)] public byte sprite_size;
		[FieldOffset(6)] public byte slave_mode; // unused
		[FieldOffset(7)] public byte enable_nmi;
    }
    PPUCTRL control = new PPUCTRL();

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    class loopy_register
    {
        [FieldOffset(0)] public byte coarse_x; // 5b
        [FieldOffset(1)] public byte coarse_y; // 5b
        [FieldOffset(2)] public byte nametable_x; // 1b
        [FieldOffset(3)] public byte nametable_y; // 1b
        [FieldOffset(4)] public byte fine_y; // 3b
        [FieldOffset(5)] public byte unused; // 1b
        public ushort DumpToUShort()
        {
            return (ushort)((coarse_x & 0x1F) | (coarse_y & 0x1F) << 5 | (nametable_x & 0x1) << 10 | (nametable_y & 0x1) << 11 | (fine_y & 0x7) << 12);
        }
        public void CopyFromUShort(ushort v)
        {
            coarse_x = (byte)(v & 0x1F);
            coarse_y = (byte)((v >> 5) & 0x1F);
            nametable_x = (byte)((v >> 10) & 0x1);
            nametable_y = (byte)((v >> 11) & 0x1);
            fine_y = (byte)((v >> 12) & 0x7);
            unused = 0;
        }        
    }

    loopy_register vram_addr = new loopy_register();
    loopy_register tram_addr = new loopy_register();

	// Pixel offset horizontally
	byte fine_x = 0x00;

	// Internal communications
	byte address_latch = 0x00;
	byte ppu_data_buffer = 0x00;

	// Pixel "dot" position information
	short scanline = 0;
	short cycle = 0;

	// Background rendering
	byte bg_next_tile_id     = 0x00;
	byte bg_next_tile_attrib = 0x00;
	byte bg_next_tile_lsb    = 0x00;
	byte bg_next_tile_msb    = 0x00;
	short bg_shifter_pattern_lo = 0x0000;
	short bg_shifter_pattern_hi = 0x0000;
	short bg_shifter_attrib_lo  = 0x0000;
	short bg_shifter_attrib_hi  = 0x0000;

    NesRom rom;
    public bool frame_complete = false;
    public bool nmi = false;

    public NesPPU()
    {
        palScreen[0x00] = new Color32(84, 84, 84, 0xFF);
        palScreen[0x01] = new Color32(0, 30, 116, 0xFF);
        palScreen[0x02] = new Color32(8, 16, 144, 0xFF);
        palScreen[0x03] = new Color32(48, 0, 136, 0xFF);
        palScreen[0x04] = new Color32(68, 0, 100, 0xFF);
        palScreen[0x05] = new Color32(92, 0, 48, 0xFF);
        palScreen[0x06] = new Color32(84, 4, 0, 0xFF);
        palScreen[0x07] = new Color32(60, 24, 0, 0xFF);
        palScreen[0x08] = new Color32(32, 42, 0, 0xFF);
        palScreen[0x09] = new Color32(8, 58, 0, 0xFF);
        palScreen[0x0A] = new Color32(0, 64, 0, 0xFF);
        palScreen[0x0B] = new Color32(0, 60, 0, 0xFF);
        palScreen[0x0C] = new Color32(0, 50, 60, 0xFF);
        palScreen[0x0D] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x0E] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x0F] = new Color32(0, 0, 0, 0xFF);

        palScreen[0x10] = new Color32(152, 150, 152, 0xFF);
        palScreen[0x11] = new Color32(8, 76, 196, 0xFF);
        palScreen[0x12] = new Color32(48, 50, 236, 0xFF);
        palScreen[0x13] = new Color32(92, 30, 228, 0xFF);
        palScreen[0x14] = new Color32(136, 20, 176, 0xFF);
        palScreen[0x15] = new Color32(160, 20, 100, 0xFF);
        palScreen[0x16] = new Color32(152, 34, 32, 0xFF);
        palScreen[0x17] = new Color32(120, 60, 0, 0xFF);
        palScreen[0x18] = new Color32(84, 90, 0, 0xFF);
        palScreen[0x19] = new Color32(40, 114, 0, 0xFF);
        palScreen[0x1A] = new Color32(8, 124, 0, 0xFF);
        palScreen[0x1B] = new Color32(0, 118, 40, 0xFF);
        palScreen[0x1C] = new Color32(0, 102, 120, 0xFF);
        palScreen[0x1D] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x1E] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x1F] = new Color32(0, 0, 0, 0xFF);

        palScreen[0x20] = new Color32(236, 238, 236, 0xFF);
        palScreen[0x21] = new Color32(76, 154, 236, 0xFF);
        palScreen[0x22] = new Color32(120, 124, 236, 0xFF);
        palScreen[0x23] = new Color32(176, 98, 236, 0xFF);
        palScreen[0x24] = new Color32(228, 84, 236, 0xFF);
        palScreen[0x25] = new Color32(236, 88, 180, 0xFF);
        palScreen[0x26] = new Color32(236, 106, 100, 0xFF);
        palScreen[0x27] = new Color32(212, 136, 32, 0xFF);
        palScreen[0x28] = new Color32(160, 170, 0, 0xFF);
        palScreen[0x29] = new Color32(116, 196, 0, 0xFF);
        palScreen[0x2A] = new Color32(76, 208, 32, 0xFF);
        palScreen[0x2B] = new Color32(56, 204, 108, 0xFF);
        palScreen[0x2C] = new Color32(56, 180, 204, 0xFF);
        palScreen[0x2D] = new Color32(60, 60, 60, 0xFF);
        palScreen[0x2E] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x2F] = new Color32(0, 0, 0, 0xFF);

        palScreen[0x30] = new Color32(236, 238, 236, 0xFF);
        palScreen[0x31] = new Color32(168, 204, 236, 0xFF);
        palScreen[0x32] = new Color32(188, 188, 236, 0xFF);
        palScreen[0x33] = new Color32(212, 178, 236, 0xFF);
        palScreen[0x34] = new Color32(236, 174, 236, 0xFF);
        palScreen[0x35] = new Color32(236, 174, 212, 0xFF);
        palScreen[0x36] = new Color32(236, 180, 176, 0xFF);
        palScreen[0x37] = new Color32(228, 196, 144, 0xFF);
        palScreen[0x38] = new Color32(204, 210, 120, 0xFF);
        palScreen[0x39] = new Color32(180, 222, 120, 0xFF);
        palScreen[0x3A] = new Color32(168, 226, 144, 0xFF);
        palScreen[0x3B] = new Color32(152, 226, 180, 0xFF);
        palScreen[0x3C] = new Color32(160, 214, 228, 0xFF);
        palScreen[0x3D] = new Color32(160, 162, 160, 0xFF);
        palScreen[0x3E] = new Color32(0, 0, 0, 0xFF);
        palScreen[0x3F] = new Color32(0, 0, 0, 0xFF);
    }
    public void loadRom(NesRom rom)
    {
        this.rom = rom;
    }
    public byte cpuRead(ushort addr, bool rdonly)
    {
        byte data = 0x00;

        if (rdonly)
        {
            // Reading from PPU registers can affect their contents
            // so this read only option is used for examining the
            // state of the PPU without changing its state. This is
            // really only used in debug mode.
            switch (addr)
            {
            case 0x0000: // Control
                data = control.DumpToByte();
                break;
            case 0x0001: // Mask
                data = mask.DumpToByte();
                break;
            case 0x0002: // Status
                data = status.DumpToByte();
                break;
            case 0x0003: // OAM Address
                break;
            case 0x0004: // OAM Data
                break;
            case 0x0005: // Scroll
                break;
            case 0x0006: // PPU Address
                break;
            case 0x0007: // PPU Data
                break;
            }
        }
        else
        {
            // These are the live PPU registers that repsond
            // to being read from in various ways. Note that not
            // all the registers are capable of being read from
            // so they just return 0x00
            switch (addr)
            {
                // Control - Not readable
            case 0x0000:
                break;
                // Mask - Not Readable
            case 0x0001: 
                break;
                // Status
            case 0x0002:
                // Reading from the status register has the effect of resetting
                // different parts of the circuit. Only the top three bits
                // contain status information, however it is possible that
                // some "noise" gets picked up on the bottom 5 bits which 
                // represent the last PPU bus transaction. Some games "may"
                // use this noise as valid data (even though they probably
                // shouldn't)
                data = (byte)((status.DumpToByte() & 0xE0) | (ppu_data_buffer & 0x1F));

                // Clear the vertical blanking flag
                status.vertical_blank = 0;

                // Reset Loopy's Address latch flag
                address_latch = 0;
                break;
                // OAM Address
            case 0x0003:
                break;
                // OAM Data
            case 0x0004: 
                break;
                // Scroll - Not Readable
            case 0x0005:
                break;
                // PPU Address - Not Readable
            case 0x0006: 
                break;
                // PPU Data
            case 0x0007: 
                // Reads from the NameTable ram get delayed one cycle, 
                // so output buffer which contains the data from the 
                // previous read request
                data = ppu_data_buffer;
                // then update the buffer for next time
                ppu_data_buffer = ppuRead(vram_addr.DumpToUShort());
                // However, if the address was in the palette range, the
                // data is not delayed, so it returns immediately
                if (vram_addr.DumpToUShort() >= 0x3F00) data = ppu_data_buffer;
                // All reads from PPU data automatically increment the nametable
                // address depending upon the mode set in the control register.
                // If set to vertical mode, the increment is 32, so it skips
                // one whole nametable row; in horizontal mode it just increments
                // by 1, moving to the next column
                if (control.increment_mode > 0) {
                    vram_addr.coarse_y += 1;
                } else {
                    vram_addr.coarse_x += 1;
                }
                break;
            }
        }
        return data;
    }

    public void cpuWrite(ushort addr, byte data)
    {
        switch (addr)
        {
        case 0x0000: // Control
            control.CopyFromByte(data);
            tram_addr.nametable_x = control.nametable_x;
            tram_addr.nametable_y = control.nametable_y;
            break;
        case 0x0001: // Mask
            mask.CopyFromByte(data);
            break;
        case 0x0002: // Status
            break;
        case 0x0003: // OAM Address
            break;
        case 0x0004: // OAM Data
            break;
        case 0x0005: // Scroll
            if (address_latch == 0)
            {
                // First write to scroll register contains X offset in pixel space
                // which we split into coarse and fine x values
                fine_x = (byte)(data & 0x07);
                tram_addr.coarse_x = (byte)(data >> 3);
                address_latch = 1;
            }
            else
            {
                // First write to scroll register contains Y offset in pixel space
                // which we split into coarse and fine Y values
                tram_addr.fine_y = (byte)(data & 0x07);
                tram_addr.coarse_y = (byte)(data >> 3);
                address_latch = 0;
            }
            break;
        case 0x0006: // PPU Address
            if (address_latch == 0)
            {
                // PPU address bus can be accessed by CPU via the ADDR and DATA
                // registers. The fisrt write to this register latches the high byte
                // of the address, the second is the low byte. Note the writes
                // are stored in the tram register...
                tram_addr.CopyFromUShort((ushort)(((data & 0x3F) << 8) | (tram_addr.DumpToUShort() & 0x00FF)));
                address_latch = 1;
            }
            else
            {
                // ...when a whole address has been written, the internal vram address
                // buffer is updated. Writing to the PPU is unwise during rendering
                // as the PPU will maintam the vram address automatically whilst
                // rendering the scanline position.
                tram_addr.CopyFromUShort((ushort)((tram_addr.DumpToUShort() & 0xFF00) | data));
                vram_addr = tram_addr;
                address_latch = 0;
            }
            break;
        case 0x0007: // PPU Data
            ppuWrite(vram_addr.DumpToUShort(), data);
            // All writes from PPU data automatically increment the nametable
            // address depending upon the mode set in the control register.
            // If set to vertical mode, the increment is 32, so it skips
            // one whole nametable row; in horizontal mode it just increments
            // by 1, moving to the next column
            if (control.increment_mode > 0) {
                vram_addr.coarse_y += 1;
            } else {
                vram_addr.coarse_x += 1;
            }            
            break;
        }
    }

    byte ppuRead(ushort addr, bool rdonly = false)
    {
        byte data = 0x00;
        addr &= 0x3FFF;

        if (rom.ppuRead(addr, ref data))
        {
        }
        else if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            // If the cartridge cant map the address, have
            // a physical location ready here
            data = tblPattern[(addr & 0x1000) >> 12, addr & 0x0FFF];
        }
        else if (addr >= 0x2000 && addr <= 0x3EFF)
        {
            addr &= 0x0FFF;

            if (rom.mirror == NesRom.MIRROR.VERTICAL)
            {
                // Vertical
                if (addr >= 0x0000 && addr <= 0x03FF)
                    data = tblName[0,addr & 0x03FF];
                if (addr >= 0x0400 && addr <= 0x07FF)
                    data = tblName[1,addr & 0x03FF];
                if (addr >= 0x0800 && addr <= 0x0BFF)
                    data = tblName[0,addr & 0x03FF];
                if (addr >= 0x0C00 && addr <= 0x0FFF)
                    data = tblName[1,addr & 0x03FF];
            }
            else if (rom.mirror == NesRom.MIRROR.HORIZONTAL)
            {
                // Horizontal
                if (addr >= 0x0000 && addr <= 0x03FF)
                    data = tblName[0,addr & 0x03FF];
                if (addr >= 0x0400 && addr <= 0x07FF)
                    data = tblName[0,addr & 0x03FF];
                if (addr >= 0x0800 && addr <= 0x0BFF)
                    data = tblName[1,addr & 0x03FF];
                if (addr >= 0x0C00 && addr <= 0x0FFF)
                    data = tblName[1,addr & 0x03FF];
            }
        }
        else if (addr >= 0x3F00 && addr <= 0x3FFF)
        {
            addr &= 0x001F;
            if (addr == 0x0010) addr = 0x0000;
            if (addr == 0x0014) addr = 0x0004;
            if (addr == 0x0018) addr = 0x0008;
            if (addr == 0x001C) addr = 0x000C;
            data = (byte)(tblPalette[addr] & (mask.grayscale > 0 ? 0x30 : 0x3F));
        }
        return data;
    }

    void ppuWrite(ushort addr, byte data)
    {
        addr &= 0x3FFF;

        if (rom.ppuWrite(addr, data))
        {
        }
        else if (addr >= 0x0000 && addr <= 0x1FFF)
        {
            tblPattern[(addr & 0x1000) >> 12, addr & 0x0FFF] = data;
        }
        else if (addr >= 0x2000 && addr <= 0x3EFF)
        {
            addr &= 0x0FFF;
            if (rom.mirror == NesRom.MIRROR.VERTICAL)
            {
                // Vertical
                if (addr >= 0x0000 && addr <= 0x03FF)
                    tblName[0,addr & 0x03FF] = data;
                if (addr >= 0x0400 && addr <= 0x07FF)
                    tblName[1,addr & 0x03FF] = data;
                if (addr >= 0x0800 && addr <= 0x0BFF)
                    tblName[0,addr & 0x03FF] = data;
                if (addr >= 0x0C00 && addr <= 0x0FFF)
                    tblName[1,addr & 0x03FF] = data;
            }
            else if (rom.mirror == NesRom.MIRROR.HORIZONTAL)
            {
                // Horizontal
                if (addr >= 0x0000 && addr <= 0x03FF)
                    tblName[0,addr & 0x03FF] = data;
                if (addr >= 0x0400 && addr <= 0x07FF)
                    tblName[0,addr & 0x03FF] = data;
                if (addr >= 0x0800 && addr <= 0x0BFF)
                    tblName[1,addr & 0x03FF] = data;
                if (addr >= 0x0C00 && addr <= 0x0FFF)
                    tblName[1,addr & 0x03FF] = data;
            }
        }
        else if (addr >= 0x3F00 && addr <= 0x3FFF)
        {
            addr &= 0x001F;
            if (addr == 0x0010) addr = 0x0000;
            if (addr == 0x0014) addr = 0x0004;
            if (addr == 0x0018) addr = 0x0008;
            if (addr == 0x001C) addr = 0x000C;
            tblPalette[addr] = data;
        }        
    }


    public void clock()
    {
        // Fake some noise for now
        // sprScreen->SetPixel(cycle - 1, scanline, palScreen[(rand() % 2) ? 0x3F : 0x30]);

        // All but 1 of the secanlines is visible to the user. The pre-render scanline
        // at -1, is used to configure the "shifters" for the first visible scanline, 0.
        if (scanline >= -1 && scanline < 240)
        {		
            if (scanline == 0 && cycle == 0)
            {
                // "Odd Frame" cycle skip
                cycle = 1;
            }

            if (scanline == -1 && cycle == 1)
            {
                // Effectively start of new frame, so clear vertical blank flag
                status.vertical_blank = 0;
            }
        }

        if (scanline == 240)
        {
            // Post Render Scanline - Do Nothing!
        }

        if (scanline >= 241 && scanline < 261)
        {
            if (scanline == 241 && cycle == 1)
            {
                // Effectively end of frame, so set vertical blank flag
                status.vertical_blank = 1;

                // If the control register tells us to emit a NMI when
                // entering vertical blanking period, do it! The CPU
                // will be informed that rendering is complete so it can
                // perform operations with the PPU knowing it wont
                // produce visible artefacts
                if (control.enable_nmi > 0) 
                    nmi = true;
            }
        }
        // Advance renderer - it never stops, it's relentless
        cycle++;
        if (cycle >= 341)
        {
            cycle = 0;
            scanline++;
            if (scanline >= 261)
            {
                scanline = -1;
                frame_complete = true;
            }
        }
    }
}