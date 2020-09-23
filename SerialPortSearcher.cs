using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading.Tasks;

namespace STM32Programmer
{
    class SerialPortSearcher
    {
        public static Lazy<SerialPortSearcher> Instance = new Lazy<SerialPortSearcher>();

        private string[] _portNames;
        private readonly Dictionary<string, string> _portInfo = new Dictionary<string, string>();

        public void Refresh()
        {
            _portNames = SerialPort.GetPortNames();
            _portInfo.Clear();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM WIN32_SerialPort"))
            {
                var ports = searcher.Get().Cast<ManagementObject>().ToList();
                foreach (var portName in _portNames)
                {
                    var port = ports.FirstOrDefault(v => v["DeviceId"].ToString() == portName);
                    if (port != null)
                    {
                        _portInfo[portName] = port["Caption"].ToString();
                    }
                }
            }
            //  
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\""))
            {
                var res = searcher.Get();
                var ports = res.Cast<ManagementObject>().ToList();
                foreach (var port in ports)
                {
                    foreach (var portName in PortNames)
                    {
                        if (((string) port["Name"]).Contains(portName))
                        {
                            _portInfo[portName] = (string) port["Name"];
                        }
                    }
                }
            }
        }

        public string[] PortNames => _portNames;
        public string this[string index] => _portInfo[index];
    }
}
