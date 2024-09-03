using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EmuRunner : MonoBehaviour
{
    Nes emu = new Nes();
    public Transform cube;
    Transform[] screenpixels;
    Dictionary<KeyCode, int> keyMapper = new Dictionary<KeyCode, int>();
    // Start is called before the first frame update
    void Start()
    {
        emu.Initialize();
        // Test();
        var fn = EditorUtility.OpenFilePanel("open rom", "", "");
        emu.LoadGame(fn);

        // // initialize screen;
        // screenpixels = new Transform[64*32];
        // for(int x = 0; x < 64; ++x) {
        //     for (int y = 0; y < 32; ++y) {
        //         screenpixels[y*64+x] = GameObject.Instantiate(cube);
        //         screenpixels[y*64+x].transform.position = new Vector3(x-32, -(y-16), 0);
        //     }
        // }
        // cube.gameObject.SetActive(false);

        // // keymapper
        // keyMapper[KeyCode.Alpha1] = 0x1;
        // keyMapper[KeyCode.Alpha2] = 0x2;
        // keyMapper[KeyCode.Alpha3] = 0x3;
        // keyMapper[KeyCode.Alpha4] = 0xC;

        // keyMapper[KeyCode.Q] = 0x4;
        // keyMapper[KeyCode.W] = 0x5;
        // keyMapper[KeyCode.E] = 0x6;
        // keyMapper[KeyCode.R] = 0xD;

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
        // if (emu.drawFlag) {
        //     for (int i = 0; i < 64*32; ++i) {
        //         screenpixels[i].gameObject.SetActive(emu.gfx[i] == 1);
        //     }
        //     emu.drawFlag = false;
        // }

        if (Input.GetKeyDown(KeyCode.Space)) {
            do {
                emu.bus.cpu.clock();
             } while(!emu.bus.cpu.IsComplete());
        }

        UpdateInput();
    }

    void UpdateInput()
    {
        foreach(var kv in keyMapper) {
            emu.key[kv.Value] = (byte)(Input.GetKey(kv.Key) ? 1 : 0);
        }
    }

    void FixedUpdate()
    {
        emu.bus.clock();
        // emu.EmulateCycle();
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
		DrawRam(2, 182, 0x8000, 16, 16); // rom
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
                var v = emu.bus.cpuRead((ushort)nAddr);
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
            "\n" + $"X: ${cpu.y:x2}" +
            "\n" + $"Y: ${cpu.y:x2}" +
            "\n" + $"STKP: ${cpu.stkp:x4}";
        GUI.Label(new Rect(x, y, 640, 100), text);       
	}

}
