namespace Confederation.Core.Devices;

/// <summary>
/// The Intel 8259 programmable interrupt controller: the PC's first one, which takes the eight
/// hardware interrupt lines (IRQ 0, the timer, to IRQ 7) and passes them to the processor one at a
/// time, the lowest-numbered first, as interrupt vectors 8 to 15.
/// </summary>
/// <remarks>
/// A device raising its line sets that IRQ's bit in the request register. If its bit in the mask
/// register is clear and no IRQ of higher priority is being serviced, the controller asks the
/// processor for an interrupt, and when the processor takes it, the bit moves to the in-service
/// register. It stays there, holding off that IRQ and every lower one, until the handler sends
/// an end-of-interrupt command (20h to port 20h). A handler that forgets gets no more interrupts.
/// The AT's second controller, for IRQ 8 to 15, isn't emulated: its mask is kept and nothing more.
/// </remarks>
internal sealed class InterruptController
{
    private byte _request;
    private byte _inService;
    private byte _mask;
    private byte _vectorBase = 0x08;
    private bool _readInService;

    // initialization: ICW1 to port 20h starts a sequence of two or three more words to port 21h
    private int _initializationStep;
    private bool _expectIcw4;

    public byte SecondMask { get; set; } = 0xFF;

    public void Raise(int irq) => _request |= (byte)(1 << irq);

    /// <summary>The IRQ the controller would give the processor now, or -1.</summary>
    private int Pending()
    {
        int ready = _request & ~_mask;
        for (int irq = 0; irq < 8; irq++)
        {
            int bit = 1 << irq;
            if ((_inService & bit) != 0)
            {
                // an IRQ in service blocks itself and everything of lower priority
                return -1;
            }

            if ((ready & bit) != 0)
            {
                return irq;
            }
        }

        return -1;
    }

    public bool HasInterrupt => Pending() >= 0;

    /// <summary>The processor takes the interrupt: its vector, its IRQ now in service.</summary>
    public int Acknowledge()
    {
        int irq = Pending();
        _request &= (byte)~(1 << irq);
        _inService |= (byte)(1 << irq);
        return _vectorBase + irq;
    }

    /// <summary>End of interrupt for the highest-priority IRQ in service, as BIOS handlers send it.</summary>
    public void EndOfInterrupt()
    {
        for (int irq = 0; irq < 8; irq++)
        {
            if ((_inService & (1 << irq)) != 0)
            {
                _inService &= (byte)~(1 << irq);
                return;
            }
        }
    }

    public byte Read(int port)
    {
        if (port == 0x21)
        {
            return _mask;
        }

        return _readInService ? _inService : _request;
    }

    public void Write(int port, byte value)
    {
        if (port == 0x20)
        {
            if ((value & 0x10) != 0)
            {
                // ICW1: start initializing; the mask is cleared
                _initializationStep = 2;
                _expectIcw4 = (value & 1) != 0;
                _mask = 0;
                _inService = 0;
            }
            else if ((value & 0x18) == 0x08)
            {
                // OCW3: which register port 20h reads
                if ((value & 2) != 0)
                {
                    _readInService = (value & 1) != 0;
                }
            }
            else if ((value & 0xE0) == 0x20)
            {
                EndOfInterrupt();
            }
            else if ((value & 0xE0) == 0x60)
            {
                // specific end of interrupt
                _inService &= (byte)~(1 << (value & 7));
            }

            return;
        }

        switch (_initializationStep)
        {
            case 2:
                _vectorBase = (byte)(value & 0xF8);
                _initializationStep = 3;
                break;
            case 3:
                // ICW3, the cascade; then ICW4 if ICW1 asked for it
                _initializationStep = _expectIcw4 ? 4 : 0;
                break;
            case 4:
                _initializationStep = 0;
                break;
            default:
                _mask = value;
                break;
        }
    }
}
