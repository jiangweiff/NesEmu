using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class EmuRunner : MonoBehaviour
{
    Nes emu = new Nes();
    public RawImage imgScreen, imgPattern1, imgPattern2;
    Transform[] screenpixels;
    Dictionary<KeyCode, int> keyMapper = new Dictionary<KeyCode, int>();
    bool running = false;
    // Start is called before the first frame update
    void Start()
    {
        emu.Initialize();
        // Test();
        var fn = EditorUtility.OpenFilePanel("open rom", "", "");
        emu.LoadGame(fn);

        imgScreen.texture = emu.bus.ppu.texScreen;
        imgPattern1.texture = emu.bus.ppu.texPatternTable[0];
        imgPattern2.texture = emu.bus.ppu.texPatternTable[1];

		// var mapAsm = emu.bus.cpu.Disassemble(0x0000, 0xFFFF);
        // keymapper
        keyMapper[KeyCode.X] = 0x80;
        keyMapper[KeyCode.Z] = 0x40;
        keyMapper[KeyCode.A] = 0x20;
        keyMapper[KeyCode.S] = 0x10;

        keyMapper[KeyCode.UpArrow] = 0x08;
        keyMapper[KeyCode.DownArrow] = 0x04;
        keyMapper[KeyCode.LeftArrow] = 0x02;
        keyMapper[KeyCode.RightArrow] = 0x01;

        // keyMapper[KeyCode.A] = 0x7;
        // keyMapper[KeyCode.S] = 0x8;
        // keyMapper[KeyCode.D] = 0x9;
        // keyMapper[KeyCode.F] = 0xE;

        // keyMapper[KeyCode.Z] = 0xA;
        // keyMapper[KeyCode.X] = 0x0;
        // keyMapper[KeyCode.C] = 0xB;
        // keyMapper[KeyCode.V] = 0xF;
    }

    // Update is called once per frame
    void Update()
    {
        emu.bus.ppu.UpdateScreenTexture();
        // emu.bus.ppu.GetPatternTable(0,0);
        // emu.bus.ppu.GetPatternTable(1,0);

        if (Input.GetKeyDown(KeyCode.Space)) {
            running = ! running;
        }

        if (running) {
            // do {
            //     emu.bus.clock();
            //  } while(!emu.bus.cpu.IsComplete());          
            do {
                emu.bus.clock();
             } while(!emu.bus.ppu.frame_complete);
             do {
                emu.bus.clock();
             } while(!emu.bus.cpu.IsComplete());
             emu.bus.ppu.frame_complete = false;
        }

        if (Input.GetKeyDown(KeyCode.F)) {
            do {
                emu.bus.clock();
             } while(!emu.bus.ppu.frame_complete);
             do {
                emu.bus.clock();
             } while(!emu.bus.cpu.IsComplete());
             emu.bus.ppu.frame_complete = false;
        }


        UpdateInput();
    }

    void UpdateInput()
    {
        emu.bus.controller[0] = 0;
        foreach(var kv in keyMapper) {
            emu.bus.controller[0] |= (byte)(Input.GetKey(kv.Key) ? kv.Value : 0);
        }
    }

    void FixedUpdate()
    {
        // emu.bus.clock();
    }

    void Test()
    {
		// Load Program (assembled at https://www.masswerk.at/6502/assembler.html)
		/*
			*=$8000
			LDX #10
			STX $0000
			LDX #3
			STX $0001
			LDY $0000
			LDA #0
			CLC
			loop
			ADC $0001
			DEY
			BNE loop
			STA $0002
			NOP
			NOP
			NOP
		*/
		
		// Convert hex string into bytes for RAM
		string ss = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";
		ushort nOffset = 0x8000;
        foreach(var h in ss.Split(' ')) {
			emu.bus.cpuRam[nOffset++] = byte.Parse(h,System.Globalization.NumberStyles.HexNumber);
        }

		// Set Reset Vector
		emu.bus.cpuRam[0xFFFC] = 0x00;
		emu.bus.cpuRam[0xFFFD] = 0x80;

		// Dont forget to set IRQ and NMI vectors if you want to play with those
				
		// Extract dissassembly
		var mapAsm = emu.bus.cpu.Disassemble(0x0000, 0xFFFF);

		// Reset
		emu.bus.reset();
    }

    void OnGUI()
    {
   		DrawRam(2, 2, 0x0000, 16, 16); // ram
		DrawVram(2, 182, 0x2000, 16, 16); // vram
		// DrawRam(2, 182, 0x8000, 16, 16); // rom
		DrawCpu(2, 350);
    }

   	void DrawRam(int x, int y, int nAddr, int nRows, int nColumns)
    {
		int nRamX = x, nRamY = y;
		for (int row = 0; row < nRows; row++)
		{
			string sOffset = $"${nAddr:x4}:";
			for (int col = 0; col < nColumns; col++)
			{
                var v = emu.bus.cpuRead((ushort)nAddr, true);
				sOffset += $" {v:x2}";
				nAddr += 1;
			}
			GUI.Label(new Rect(nRamX, nRamY, 640, 30), sOffset);
			nRamY += 10;
		}
    }

   	void DrawVram(int x, int y, int nAddr, int nRows, int nColumns)
    {
		int nRamX = x, nRamY = y;
		for (int row = 0; row < nRows; row++)
		{
			string sOffset = $"${nAddr:x4}:";
			for (int col = 0; col < nColumns; col++)
			{
                var v = emu.bus.ppu.ppuRead((ushort)nAddr, true);
				sOffset += $" {v:x2}";
				nAddr += 1;
			}
			GUI.Label(new Rect(nRamX, nRamY, 640, 30), sOffset);
			nRamY += 10;
		}
    }

   	void DrawCpu(int x, int y)
	{
        var cpu = emu.bus.cpu;
        Rect rc = new Rect(x,y,640,80);
        string text = $"<color=white>STATUS:</color>" + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.N) > 0 ? "<color=green>N</color>" : "<color=red>N</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.V) > 0 ? "<color=green>V</color>" : "<color=red>V</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.U) > 0 ? "<color=green>U</color>" : "<color=red>U</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.B) > 0 ? "<color=green>B</color>" : "<color=red>B</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.D) > 0 ? "<color=green>D</color>" : "<color=red>D</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.I) > 0 ? "<color=green>I</color>" : "<color=red>I</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.Z) > 0 ? "<color=green>Z</color>" : "<color=red>Z</color>") + 
            " " + (cpu.GetFlag(NesCpu.FLAGS6502.C) > 0 ? "<color=green>C</color>" : "<color=red>C</color>") + 
            "\n" + $"PC: ${cpu.pc:x4}" +
            "\n" + $"A: ${cpu.a:x2}" +
            "\n" + $"X: ${cpu.x:x2}" +
            "\n" + $"Y: ${cpu.y:x2}" +
            "\n" + $"STKP: ${cpu.stkp:x4}";
        GUI.Label(new Rect(x, y, 640, 100), text);       
	}

}
