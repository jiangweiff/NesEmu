using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class Nes
{
    public byte[] key = new byte[16];
    public NesBus bus;
    NesRom rom;

    public void Initialize()
    {
        bus = new NesBus();
    }

    public void LoadGame(string filename)
    {
        rom = new NesRom();
        rom.Load(filename);
        bus.loadRom(rom);
        bus.reset();
    }

}
