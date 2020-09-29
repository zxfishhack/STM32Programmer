using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using STM32Programmer.Annotations;
using STM32Programmer.TimingImpl;

namespace STM32Programmer
{
    sealed class Vm : ApplicationSettingsBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        private string _runLog = "", _programLog = "", _statusText = "空闲";
        private System.IO.Ports.SerialPort _instanceSerialPort = null;
        private int _progressTotal = 1, _progressValue = 0;
        private bool _Programming = false;

        public ObservableCollection<int> BaudRateList { get; } = new ObservableCollection<int>{115200, 57600};
        public ObservableCollection<string> SerialPortList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<Tuple<String, StopBits>> StopBitList { get; } = new ObservableCollection<Tuple<String, StopBits>>
        {
            new Tuple<String, StopBits>("1位", StopBits.One),
            new Tuple<String, StopBits>("1.5位", StopBits.OnePointFive),
            new Tuple<String, StopBits>("2位", StopBits.Two),
        };
        public ObservableCollection<Tuple<String, int>> DataBitList { get; } = new ObservableCollection<Tuple<String, int>>
        {
            new Tuple<String, int>("7位", 7),
            new Tuple<String, int>("8位", 8),
        };
        public ObservableCollection<Tuple<String, Parity>> ParityBitList { get; } = new ObservableCollection<Tuple<string, Parity>>
        {
            new Tuple<String, Parity>("无校验", Parity.None),
            new Tuple<String, Parity>("奇校验", Parity.Odd),
            new Tuple<String, Parity>("偶校验", Parity.Even),
            new Tuple<String, Parity>("0 校验", Parity.Space),
            new Tuple<String, Parity>("1 校验", Parity.Mark),
        };

        public ObservableCollection<ITimingImpl> TimingList { get; } = new ObservableCollection<ITimingImpl>
        {
            new StepTiming("110-1-1", "100-0-1"),
            new StepTiming("110-1", "110-1"),
        };

        [UserScopedSetting()]
        [DefaultSettingValue("115200")]
        public int BaudRate
        {
            get { return (int) this[nameof(BaudRate)]; }
            set
            {
                this[nameof(BaudRate)] = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        public string SerialPort
        {
            get { return (string) this[nameof(SerialPort)]; }
            set
            {
                this[nameof(SerialPort)] = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("0")]
        public int StopBitIdx
        {
            get { return (int)this[nameof(StopBitIdx)]; }
            set
            {
                this[nameof(StopBitIdx)] = value;
                OnPropertyChanged();
            }
        }

        public StopBits StopBit => StopBitList[StopBitIdx].Item2;

        [UserScopedSetting()]
        [DefaultSettingValue("0")]
        public int DataBitIdx
        {
            get { return (int)this[nameof(DataBitIdx)]; }
            set
            {
                this[nameof(DataBitIdx)] = value;
                OnPropertyChanged();
            }
        }

        public int DataBit => DataBitList[DataBitIdx].Item2;

        [UserScopedSetting()]
        [DefaultSettingValue("0")]
        public int ParityBitIdx
        {
            get { return (int)this[nameof(ParityBitIdx)]; }
            set
            {
                this[nameof(ParityBitIdx)] = value;
                OnPropertyChanged();
            }
        }

        public Parity ParityBit => ParityBitList[StopBitIdx].Item2;

        [UserScopedSetting()]
        [DefaultSettingValue("0")]
        public int DownloadTimingIdx
        {
            get { return (int)this[nameof(DownloadTimingIdx)]; }
            set
            {
                this[nameof(DownloadTimingIdx)] = value;
                OnPropertyChanged();
            }
        }

        public ITimingImpl DownloadTiming => TimingList[DownloadTimingIdx];

        [UserScopedSetting()]
        [DefaultSettingValue("0")]
        public int RestartTimingIdx
        {
            get { return (int)this[nameof(RestartTimingIdx)]; }
            set
            {
                this[nameof(RestartTimingIdx)] = value;
                OnPropertyChanged();
            }
        }

        public ITimingImpl RestartTiming => TimingList[RestartTimingIdx];

        public string RunLog
        {
            get { return _runLog; }
            set
            {
                _runLog = value;
                OnPropertyChanged();
            }
        }

        public string ProgrammerLog
        {
            get { return _programLog; }
            set
            {
                _programLog = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        public string FileName
        {
            get { return (string)this[nameof(FileName)]; }
            set
            {
                this[nameof(FileName)] = value;
                OnPropertyChanged();
            }
        }

        public System.IO.Ports.SerialPort SerialPortInstance
        {
            get { return _instanceSerialPort; }
            set
            {
                _instanceSerialPort = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("True")]
        [SettingsSerializeAs(SettingsSerializeAs.String)]
        public bool Check
        {
            get
            {
                return (bool)this[nameof(Check)];
            }
            set
            {
                this[nameof(Check)] = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("True")]
        [SettingsSerializeAs(SettingsSerializeAs.String)]
        public bool Exec
        {
            get
            {
                return (bool)this[nameof(Exec)];
            }
            set
            {
                this[nameof(Exec)] = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("True")]
        [SettingsSerializeAs(SettingsSerializeAs.String)]
        public bool AutoOpenSerial
        {
            get
            {
                return (bool)this[nameof(AutoOpenSerial)];
            }
            set
            {
                this[nameof(AutoOpenSerial)] = value;
                OnPropertyChanged();
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("False")]
        [SettingsSerializeAs(SettingsSerializeAs.String)]
        public bool EnableIsHigh
        {
            get
            {
                return (bool)this[nameof(EnableIsHigh)];
            }
            set
            {
                this[nameof(EnableIsHigh)] = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get
            {
                return _statusText;
            }
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public int ProgressTotal
        {
            get
            {
                return _progressTotal;
            }
            set
            {
                _progressTotal = value;
                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get
            {
                return _progressValue;
            }
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        public bool Programming
        {
            get
            {
                return _Programming;
            }
            set
            {
                _Programming = value;
                OnPropertyChanged();
            }
        }

        public void RefreshSerialPort()
        {
            SerialPortSearcher.Instance.Value.Refresh();
            var ports = SerialPortSearcher.Instance.Value.PortNames.ToList();
            ports.Sort(String.CompareOrdinal);

            SerialPortList.Clear();
            ;
            foreach (var portName in ports)
            {
                SerialPortList.Add(portName);
            }

            if (SerialPortList.Count > 0)
            {
                if (SerialPortList.IndexOf(SerialPort) <= 0)
                {
                    SerialPort = SerialPortList[0];
                }
            }
            else
            {
                SerialPort = "";
            }
        }

        public Vm()
        {
            RefreshSerialPort();
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
