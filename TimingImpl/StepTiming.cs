using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace STM32Programmer.TimingImpl
{
    class StepTiming : ITimingImpl
    {
        private string _dtr, _rts;
        public override string ToString()
        {
            return String.Format("DTR {0} RTS {1}", _dtr, _rts);
        }

        public StepTiming(string dtr, string rts)
        {
            _dtr = dtr;
            _rts = rts;
            if (dtr.IndexOf('-') != rts.IndexOf('-'))
            {
                throw new Exception("DTR/RTS等待时机不同步");
            }

            if (dtr.Length != rts.Length)
            {
                throw new Exception("DTR/RTS长度不一致");
            }
        }
        public void Run(SerialPort serialPort, bool enableIsHigh)
        {
            for (var i = 0; i < _dtr.Length; i++)
            {
                if (i == 0 || _dtr[i] != _dtr[i - 1])
                {
                    switch (_dtr[i])
                    {
                        case '1':
                            serialPort.DtrEnable = enableIsHigh;
                            break;
                        case '0':
                            serialPort.DtrEnable = !enableIsHigh;
                            break;
                    }
                }

                if (i == 0 || _rts[i] != _rts[i - 1])
                {
                    switch (_rts[i])
                    {
                        case '1':
                            serialPort.RtsEnable = enableIsHigh;
                            break;
                        case '0':
                            serialPort.RtsEnable = !enableIsHigh;
                            break;
                    }
                }

                if (_rts[i] == '-')
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }
            }
        }
    }
}
