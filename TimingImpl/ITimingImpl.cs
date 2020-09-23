using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace STM32Programmer.TimingImpl
{
    interface ITimingImpl
    {
        void Run(SerialPort serialPort, bool enableIsHigh);
    }
}
