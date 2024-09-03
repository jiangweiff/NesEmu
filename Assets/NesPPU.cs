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
                val |= (byte)(data[i] > 0 ? 1 << 7-i : 0);
            }
            return val;
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
        [FieldOffset(0)] public byte coarse_x;
        [FieldOffset(1)] public byte coarse_y;
        [FieldOffset(2)] public byte nametable_x;
        [FieldOffset(3)] public byte nametable_y;
        [FieldOffset(4)] public byte fine_y;
        [FieldOffset(5)] public byte unused;
        public ushort DumpToUShort()
        {
            return (ushort)(coarse_x << 11 | coarse_y << 6 | nametable_x << 5 | nametable_y << 4 | fine_y << 1);
        }
    }

    loopy_register vram_addr = new loopy_register();
    loopy_register tram_addr = new loopy_register();

    NesRom rom;
    short scanline = 0;
	short cycle = 0;
    bool frame_complete = false;
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

        vram_addr.coarse_x = 1;
        vram_addr.coarse_y = 2;
        vram_addr.nametable_y = 1;
        vram_addr.fine_y = 3;
        var aa = vram_addr.DumpToUShort();
        Debug.Log(aa);

    }
    public void loadRom(NesRom rom)
    {
        this.rom = rom;
    }
    public byte cpuRead(ushort addr, bool rdonly)
    {
        byte data = 0x00;

        switch (addr)
        {
        case 0x0000: // Control
            break;
        case 0x0001: // Mask
            break;
        case 0x0002: // Status
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

        return data;
    }

    public void cpuWrite(ushort addr, byte data)
    {
        switch (addr)
        {
        case 0x0000: // Control
            break;
        case 0x0001: // Mask
            break;
        case 0x0002: // Status
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

    byte ppuRead(ushort addr, bool rdonly)
    {
        byte data = 0x00;
        addr &= 0x3FFF;

        if (rom.ppuRead(addr, ref data))
        {
        }

        return data;
    }

    void ppuWrite(ushort addr, byte data)
    {
        addr &= 0x3FFF;

        if (rom.ppuWrite(addr, data))
        {
        }
    }


    public void clock()
    {
        // Fake some noise for now
        // sprScreen->SetPixel(cycle - 1, scanline, palScreen[(rand() % 2) ? 0x3F : 0x30]);

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