using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace STM32Programmer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Task _serialPortRead = null;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void RunLogRead()
        {
            try
            {
                while (true)
                {
                    Vm.RunLog += Vm.SerialPortInstance.ReadLine() + "\n";
                    RunLogBox.Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(50);
                        RunLogBox.ScrollToEnd();
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async void ToggleSerialPort(object sender, RoutedEventArgs e)
        {
            if (Vm.SerialPortInstance != null)
            {
                Vm.SerialPortInstance.Close();
                if (_serialPortRead != null)
                {
                    await _serialPortRead;
                }
                Vm.SerialPortInstance = null;
                _serialPortRead = null;
                return;
            }
            Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, Vm.BaudRate, Vm.ParityBit, Vm.DataBit, Vm.StopBit);
            Vm.SerialPortInstance.RtsEnable = false;
            Vm.SerialPortInstance.DtrEnable = false;
            Vm.SerialPortInstance.NewLine = "\r\n";
            Vm.SerialPortInstance.Open();
            _serialPortRead = Task.Run(() => RunLogRead());
        }

        private void RefreshSerialPort(object sender, RoutedEventArgs e)
        {
            Vm.RefreshSerialPort();
        }

        private void McuRestart(object sender, RoutedEventArgs e)
        {
            if (Vm.SerialPortInstance != null)
            {
                Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
            }
            else
            {
                var inst = new SerialPort(Vm.SerialPort, Vm.BaudRate, Vm.ParityBit, Vm.DataBit, Vm.StopBit);
                inst.Open();
                Vm.RestartTiming.Run(inst, Vm.EnableIsHigh);
                inst.Close();
            }
        }

        private void AppendLog(string log)
        {
            Vm.ProgrammerLog += log + "\r\n";
            ProgrammerLogBox.ScrollToEnd();
        }

        private void ProgressReport(string text, int progress, int total)
        {
            Vm.StatusText = text;
            Vm.ProgressTotal = total;
            Vm.ProgressValue = progress;
        }

        private async void McuProgram(object sender, RoutedEventArgs e)
        {
            Vm.Programming = true;
            var autoOpen = Vm.AutoOpenSerial;
            if (Vm.SerialPortInstance != null)
            {
                Vm.SerialPortInstance.Close();
                if (_serialPortRead != null)
                {
                    await _serialPortRead;
                    _serialPortRead = null;
                }

                autoOpen = true;
            }

            var programmer = new Programmer.STM32Programmer();

            Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, programmer.BaudRate, programmer.ParityBit, programmer.DataBit, programmer.StopBit);
            Vm.SerialPortInstance.Open();
            
            programmer.Inst = Vm.SerialPortInstance;
            programmer.AppendLog += AppendLog;
            programmer.ProgressReport += ProgressReport;

            // 重启进入下载模式
            Vm.DownloadTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
            do
            {
                // 连接
                if (!await programmer.Probe())
                {
                    AppendLog("连接MCU失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 获取MCU信息
                if (!await programmer.GetMcuInfo())
                {
                    AppendLog("获取MCU信息失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 清除芯片
                if (!await programmer.Clear())
                {
                    AppendLog("清除MCU失败");
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 下载
                if (!await programmer.Flash(Vm.FileName))
                {
                    AppendLog("下载程序失败");
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 校验
                if (Vm.Check && !await programmer.Check())
                {
                    AppendLog("校验程序失败");
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }

                // 执行
                if (Vm.Exec && !await programmer.Run())
                {
                    AppendLog("执行程序失败");
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }

                Vm.SerialPortInstance.Close();
                Vm.SerialPortInstance = null;

                if (autoOpen)
                {
                    Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, Vm.BaudRate, Vm.ParityBit, Vm.DataBit, Vm.StopBit);
                    Vm.SerialPortInstance.Open();
                    _serialPortRead = Task.Run(() => RunLogRead());
                }
            } while (false);

            Vm.Programming = false;
            Vm.StatusText = "空闲";
        }

        private async void McuGetInfo(object sender, RoutedEventArgs e)
        {
            Vm.StatusText = "获取芯片信息中...";
            bool autoOpen = false;
            if (Vm.SerialPortInstance != null)
            {
                Vm.SerialPortInstance.Close();
                if (_serialPortRead != null)
                {
                    await _serialPortRead;
                    autoOpen = true;
                }
            }

            var programmer = new Programmer.STM32Programmer();

            Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, programmer.BaudRate, programmer.ParityBit, programmer.DataBit, programmer.StopBit);
            Vm.SerialPortInstance.Open();

            programmer.Inst = Vm.SerialPortInstance;
            programmer.AppendLog += AppendLog;
            programmer.ProgressReport += ProgressReport;

            // 重启进入下载模式
            Vm.DownloadTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);

            do
            {
                // 连接
                if (!await programmer.Probe())
                {
                    AppendLog("连接MCU失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 获取MCU信息
                if (!await programmer.GetMcuInfo())
                {
                    AppendLog("获取MCU信息失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }

                Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);

                Vm.SerialPortInstance.Close();
                Vm.SerialPortInstance = null;
                if (autoOpen)
                {
                    Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, Vm.BaudRate, Vm.ParityBit, Vm.DataBit, Vm.StopBit);
                    Vm.SerialPortInstance.Open();
                    _serialPortRead = Task.Run(() => RunLogRead());
                }
            } while (false);

            Vm.StatusText = "空闲";
        }

        private async void McuClear(object sender, RoutedEventArgs e)
        {
            if (Vm.SerialPortInstance != null)
            {
                Vm.SerialPortInstance.Close();
                if (_serialPortRead != null)
                {
                    await _serialPortRead;
                }
            }

            var programmer = new Programmer.STM32Programmer();

            Vm.SerialPortInstance = new SerialPort(Vm.SerialPort, programmer.BaudRate, programmer.ParityBit, programmer.DataBit, programmer.StopBit);
            Vm.SerialPortInstance.Open();

            programmer.Inst = Vm.SerialPortInstance;
            programmer.AppendLog += AppendLog;
            programmer.ProgressReport += ProgressReport;

            // 重启进入下载模式
            Vm.DownloadTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);

            do
            {
                // 连接
                if (!await programmer.Probe())
                {
                    AppendLog("连接MCU失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 获取MCU信息
                if (!await programmer.GetMcuInfo())
                {
                    AppendLog("获取MCU信息失败");
                    Vm.RestartTiming.Run(Vm.SerialPortInstance, Vm.EnableIsHigh);
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }
                // 清除芯片
                if (!await programmer.Clear())
                {
                    AppendLog("清除MCU失败");
                    Vm.SerialPortInstance.Close();
                    Vm.SerialPortInstance = null;
                    break;
                }

                Vm.SerialPortInstance.Close();
                Vm.SerialPortInstance = null;
            } while (false);

            Vm.StatusText = "空闲";
        }

        private void SelectHexFile(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "打开文件...",
                Filter = "HEX 文件(*.hex)|*.hex|所有文件|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                Vm.FileName = dlg.FileName;
            }
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Vm.Save();
        }
    }
}
