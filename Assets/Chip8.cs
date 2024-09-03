using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class Chip8
{
    byte[] chip8_fontset = new byte[80]
    { 
        0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
        0x20, 0x60, 0x20, 0x20, 0x70, // 1
        0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
        0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
        0x90, 0x90, 0xF0, 0x10, 0x10, // 4
        0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
        0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
        0xF0, 0x10, 0x20, 0x40, 0x40, // 7
        0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
        0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
        0xF0, 0x90, 0xF0, 0x90, 0x90, // A
        0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
        0xF0, 0x80, 0x80, 0x80, 0xF0, // C
        0xE0, 0x90, 0x90, 0x90, 0xE0, // D
        0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
        0xF0, 0x80, 0xF0, 0x80, 0x80  // F
    };
    ushort opcode;
    byte[] memory = new byte[4096];
    byte[] V = new byte[16];
    ushort I;
    ushort pc;
    public byte[] gfx = new byte[64*32];
    public bool drawFlag = false;
    byte delay_timer;
    byte sound_timer;

    ushort[] stack = new ushort[16];
    ushort sp;
    public byte[] key = new byte[16];

    public void Initialize()
    {
        pc = 0x200;
        opcode = 0;
        I = 0;
        sp = 0;

        // load fontset
        for(int i = 0; i < 80; ++i) {
            memory[i] = chip8_fontset[i];
        }
    }

    public void LoadGame(string filename)
    {
        var buffer = File.ReadAllBytes(filename);
        Array.Copy(buffer, 0, memory, 512, buffer.Length);
    }

    public void EmulateCycle()
    {
        opcode = (ushort)(memory[pc] << 8 | memory[pc+1]);

        switch(opcode & 0xF000) {
            case 0xA000: // ANNN: Sets I to the address NNN
                I = (ushort)(opcode & 0x0FFF);
                pc += 2;
            break;

            case 0x0000:
                switch(opcode & 0x000F) {
                    case 0x0000: // 0x00E0: Clears the screen
                        for(int i = 0; i < gfx.Length; ++i) {
                            gfx[i] = 0;
                        }
                        pc += 2;
                    break;
                    case 0x000E: // 0x00EE: Returns from subroutine  
                        pc = stack[--sp];
                        pc += 2;
                    break;
                    default:
                        Debug.Log($"Unknown opcode [0x0000]:0x{opcode:X}");
                    break;
                }
            break;
            
            case 0x1000: // jump to address NNN	
                pc = (ushort)(opcode & 0x0FFF);
                break;

            case 0x2000:
                stack[sp] = pc;
                ++sp;
                pc = (ushort)(opcode & 0x0FFF);
            break;
            
            case 0x3000: // 3XRR, skip next instruction if register VX == constant RR
                if (V[(opcode & 0x0F00) >> 8] == (byte)(opcode & 0x00FF)) {
                    pc += 4;
                } else {
                    pc += 2;
                }
            break;

            case 0x4000: // 4XRR, skip next instruction if register VX != constant RR
                if (V[(opcode & 0x0F00) >> 8] != (byte)(opcode & 0x00FF)) {
                    pc += 4;
                } else {
                    pc += 2;
                }
            break;
            
            case 0x5000: // 5XY0, skip next instruction if register VX == register VY
                if (V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4]) {
                    pc += 4;
                } else {
                    pc += 2;
                }
            break;

            case 0x6000: // 6XRR move constant RR to register VX	
                V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);
                pc += 2;
            break;
            
            case 0x7000: // 7XRR add constant RR to register VX	
                V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);
                pc += 2;
            break;

            case 0x8000:
                switch(opcode & 0x000F) {
                    case 0x0000: // 8XY0 move register VY into VX
                        V[(opcode & 0x0F00) >> 8] = V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0001: // 8XY1, or register VY with register VX, store result into register VX	
                        V[(opcode & 0x0F00) >> 8] |= V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0002: // 8XY2, and register VY with register VX, store result into register VX	
                        V[(opcode & 0x0F00) >> 8] &= V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0003: // 8XY3, exclusive or register VY with register VX, store result into register VX	
                        V[(opcode & 0x0F00) >> 8] ^= V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0004: // 8XY4, add register VY to VX, store result in register VX, carry stored in register VF
                        if (V[(opcode & 0x00F0) >> 4] > (0xFF - V[(opcode & 0x0F00) >> 8])) {
                            V[0xF] = 1;                            
                        } else {
                            V[0xF] = 0;
                        }
                        V[(opcode & 0x0F00) >> 8] += V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0005: // 8XY5, subtract register VY from VX, borrow stored in register VF
                        if (V[(opcode & 0x00F0) >> 4] > V[(opcode & 0x0F00) >> 8]) {
                            V[0xF] = 1;
                        } else {
                            V[0xF] = 0;
                        }
                        V[(opcode & 0x0F00) >> 8] -= V[(opcode & 0x00F0) >> 4];
                        pc += 2;
                    break;
                    case 0x0006: // 8X06, shift register VX right, bit 0 goes into register VF	
                        V[0xF] = (byte)(V[(opcode & 0x0F00) >> 8] & 0x0001);
                        V[(opcode & 0x0F00) >> 8] >>= 1;
                        pc += 2;
                    break;
                    case 0x0007: // 8XY7, subtract register VX from register VY, result stored in register VX	
                        if (V[(opcode & 0x0F00) >> 8] > V[(opcode & 0x00F0) >> 4]) {
                            V[0xF] = 1;
                        } else {
                            V[0xF] = 0;
                        }
                        V[(opcode & 0x0F00) >> 8] = (byte)(V[(opcode & 0x00F0) >> 4] - V[(opcode & 0x0F00) >> 8]);
                        pc += 2;
                    break;
                    case 0x000E: // 8X0E, shift register VX left, bit 7 stored into register VF	
                        V[0xF] = (byte)(V[(opcode & 0x0F00) >> 8] >> 7);
                        V[(opcode & 0x0F00) >> 8] <<= 1;
                        pc += 2;
                    break;
                    default:
                        Debug.Log($"Unknown opcode [0x8000]:0x{opcode:X}");
                    break;
                }
            break;

            case 0x9000: // 9XY0, skip next instruction if register VX != register VY	
                if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00F0) >> 4]) {
                    pc += 4;
                } else {
                    pc += 2;
                }
            break;
            
            case 0xC000: // CXKK, register VX = random number AND KK	
                V[(opcode & 0x0F00) >> 8] = (byte)(UnityEngine.Random.Range(0,255) & (opcode & 0x00FF));
                pc += 2;
            break;

            case 0xD000:
                ushort x = V[(opcode & 0x0F00) >> 8];
                ushort y = V[(opcode & 0x00F0) >> 4];
                ushort height = (ushort)(opcode & 0x000F);
                ushort pixel;
                V[0xF] = 0;
                for(int yline = 0; yline < height; yline++) {
                    pixel = memory[I+yline];
                    for(int xline = 0; xline < 8; xline++) {
                        if((pixel & (0x80 >> xline)) != 0) {
                            if (gfx[x + xline + ((y+yline)*64)] == 1) {
                                V[0xF] = 1;
                            }
                            gfx[x + xline + ((y+yline)*64)] ^= 1;                            
                        }
                    }
                }
                drawFlag = true;
                pc += 2;
            break;
            case 0xE000:
                switch(opcode & 0x00FF)
                {
                    case 0x009E: // EX9E: Skips the next instruction if the key stored in VX is pressed
                        if(key[V[(opcode & 0x0F00) >> 8]] != 0)
                            pc += 4;
                        else
                            pc += 2;
                    break;
                    
                    case 0x00A1: // EXA1: Skips the next instruction if the key stored in VX isn't pressed
                        if(key[V[(opcode & 0x0F00) >> 8]] == 0)
                            pc += 4;
                        else
                            pc += 2;
                    break;

                    default:
                        Debug.Log($"Unknown opcode [0xE000]: 0x{opcode:X}");
                    break;
                }
            break;
            case 0xF000:
                switch(opcode & 0x00FF) {
                    case 0x0007: //fr07 get delay timer into vr	
                        V[(opcode & 0x0F00) >> 8] = delay_timer;
                        pc += 2;
                    break;
                    case 0x000A: // FX0A: A key press is awaited, and then stored in VX		
                        bool keyPress = false;
                        for(int i = 0; i < 16; ++i)
                        {
                            if(key[i] != 0)
                            {
                                V[(opcode & 0x0F00) >> 8] = (byte)i;
                                keyPress = true;
                            }
                        }
                        // If we didn't received a keypress, skip this cycle and try again.
                        if(keyPress) {
                            pc += 2;
                        }
                    break;                    
                    case 0x0015: //fr15 set the delay timer to vr
                        delay_timer = V[(opcode & 0x0F00) >> 8];
                        pc += 2;
                    break;
                    case 0x0018: //fr15 set the sound timer to vr
                        sound_timer = V[(opcode & 0x0F00) >> 8];
                        pc += 2;
                    break;
                    case 0x001E: //fr1e, add register vr to the index register	
                        I += V[(opcode & 0x0F00) >> 8];
                        pc += 2;
                    break;
                    case 0x0055: //FX55: Stores V0 to VX in memory starting at address I					
                        for (int i = 0; i <= ((opcode & 0x0F00) >> 8); ++i) {
                            memory[I + i] = V[i];	
                        }
                        // On the original interpreter, when the operation is done, I = I + X + 1.
                        I = (ushort)(I + ((opcode & 0x0F00) >> 8) + 1);
                        pc += 2;
                    break;                        
                    case 0x0065: // fr65, load registers v0-vr from location I onwards
                        for(int i = 0; i <= ((opcode & 0x0F00) >> 8); ++i) {
                            V[i] = memory[I + i];
                        }
                        I = (ushort)(I + ((opcode & 0x0F00) >> 8) + 1);
                        pc += 2;
                    break;
                    default:
                        Debug.Log($"Unknown opcode [0xF000]:0x{opcode:X}");
                    break;                        
                }
            break;

            default:
                Debug.Log($"Unknown opcode:0x{opcode:X}");
            break;
        }

        if (delay_timer > 0) {
            --delay_timer;
        }

        if (sound_timer > 0) {
            if (sound_timer == 1) {
                Debug.Log("BEEP!");
            }
            --sound_timer;
        }

    }
}
