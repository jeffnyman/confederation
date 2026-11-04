using System.Numerics;

namespace Confederation.Core.Processors;

// The two-byte opcodes, 0Fh and a second byte: mostly the 386's additions.
public sealed partial class Intel386
{
    private void RunExtended(byte opcode)
    {
        int size = OperandSize;
        switch (opcode)
        {
            // CLTS: clears the task-switched bit of CR0
            case 0x06: Cr0 &= ~0x8u; return;

            case >= 0x80 and <= 0x8F:
            {
                int displacement = (int)FetchImmediate(size);
                if (Condition(opcode & 0x0F))
                {
                    JumpRelative(displacement);
                }

                return;
            }

            case >= 0x90 and <= 0x9F:
                DecodeModRm();
                WriteRm(1, Condition(opcode & 0x0F) ? 1u : 0);
                return;

            case 0xA0: Push(_selectors[FS], size); return;
            case 0xA1: PopSegment(FS); return;
            case 0xA8: Push(_selectors[GS], size); return;
            case 0xA9: PopSegment(GS); return;

            case 0xA3: BitTest(0, size, immediate: false); return;
            case 0xAB: BitTest(1, size, immediate: false); return;
            case 0xB3: BitTest(2, size, immediate: false); return;
            case 0xBB: BitTest(3, size, immediate: false); return;
            case 0xBA:
                DecodeModRm();
                if (_reg < 4)
                {
                    throw InvalidOpcode();
                }

                BitTest(_reg - 4, size, immediate: true);
                return;

            case 0xA4:
            case 0xA5:
            case 0xAC:
            case 0xAD:
            {
                DecodeModRm();
                uint destination = ReadRm(size);
                int count = (opcode & 1) == 0 ? FetchByte() : (int)GetReg(ECX, 1);
                if ((count & 0x1F) != 0)
                {
                    WriteRm(size, DoubleShift(left: opcode < 0xA8, destination, GetReg(_reg, size), count, size));
                }

                return;
            }

            case 0xAF:
                DecodeModRm();
                SetReg(_reg, size, SignedMultiplyTruncated(GetReg(_reg, size), ReadRm(size), size));
                return;

            case 0xB2: LoadFarPointer(SS, size); return;
            case 0xB4: LoadFarPointer(FS, size); return;
            case 0xB5: LoadFarPointer(GS, size); return;

            // MOVZX and MOVSX: a byte or word, zero- or sign-extended into a larger register
            case 0xB6: DecodeModRm(); SetReg(_reg, size, ReadRm(1)); return;
            case 0xB7: DecodeModRm(); SetReg(_reg, size, ReadRm(2)); return;
            case 0xBE: DecodeModRm(); SetReg(_reg, size, (uint)(sbyte)ReadRm(1)); return;
            case 0xBF: DecodeModRm(); SetReg(_reg, size, (uint)(short)ReadRm(2)); return;

            case 0xBC:
            case 0xBD:
            {
                // BSF and BSR: the lowest or highest set bit's number. With no bit set, zero is
                // set and the destination is left alone
                DecodeModRm();
                uint value = ReadRm(size);
                Set(Flag.Zero, value == 0);
                if (value != 0)
                {
                    SetReg(_reg, size, (uint)(opcode == 0xBC
                        ? BitOperations.TrailingZeroCount(value)
                        : 31 - BitOperations.LeadingZeroCount(value)));
                }

                return;
            }

            // the system instructions (descriptor tables, the machine status word, control and
            // debug registers) and LOADALL belong to protected mode, which comes later
            case 0x00:
            case 0x01:
            case 0x07:
            case >= 0x20 and <= 0x26:
                throw new NotSupportedException($"The 386 instruction 0F {opcode:X2} isn't emulated yet.");

            default: throw InvalidOpcode();
        }
    }

    /// <summary>
    /// BT, BTS, BTR and BTC: copy a bit into carry, and leave it, set it, clear it or flip it.
    /// With a register holding the bit number and a memory operand, the number is signed and can
    /// reach outside the operand: the address moves by whole operands to the one holding the bit.
    /// </summary>
    private void BitTest(int operation, int size, bool immediate)
    {
        if (!immediate)
        {
            DecodeModRm();
        }

        int bits = 8 * size;
        uint bitNumber = immediate ? FetchByte() : GetReg(_reg, size);

        if (_mod != 3 && !immediate)
        {
            long signed = SignExtend(bitNumber, size);
            long step = (signed >> (size == 2 ? 4 : 5)) * size;
            _eaOffset = _address32 ? _eaOffset + (uint)step : (uint)(_eaOffset + step) & 0xFFFF;
        }

        int bit = (int)(bitNumber & (uint)(bits - 1));
        uint value = ReadRm(size);
        uint mask = 1u << bit;
        Set(Flag.Carry, (value & mask) != 0);

        switch (operation)
        {
            case 1: WriteRm(size, value | mask); break;
            case 2: WriteRm(size, value & ~mask); break;
            case 3: WriteRm(size, value ^ mask); break;
        }
    }
}
