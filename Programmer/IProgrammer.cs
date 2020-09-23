using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM32Programmer.Programmer
{
    internal abstract class Programmer
    {
        public abstract Task<bool> Probe();
        public abstract Task<bool> Clear();
        public abstract Task<bool> Flash(string fn);
        public abstract Task<bool> Run();
        public abstract Task<bool> GetMcuInfo();
        public abstract Task<bool> Check();

        public delegate void AppendLogFunc(string log);
        public delegate void ProgressReportFunc(string text, int progress, int total);

        public SerialPort Inst = null;
        public AppendLogFunc AppendLog = null;
        public ProgressReportFunc ProgressReport = null;

        public int BaudRate = 115200;
        public Parity ParityBit = Parity.None;
        public int DataBit = 0;
        public StopBits StopBit = StopBits.None;
    }
}
