using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace STM32Programmer.Programmer
{
    class STM32Programmer : Programmer
    {
        private int _tryTime = 400;
        private const int ACK = 0x79;
        private const int NACK = 0x1f;

        //命令字定义
        private const byte ProbeCmd = 0x7f;
        private const byte GetCmd = 0x00;
        private const byte GetVersionCmd = 0x01;
        private const byte GetIdCmd = 0x02;
        private const byte ReadCmd = 0x11;
        private const byte GoCmd = 0x21;
        private const byte WriteCmd = 0x31;
        private const byte EraseCmd = 0x43;
        private const byte EEraseCmd = 0x44;
        private const byte WriteProtectCmd = 0x63;
        private const byte WriteUnProtectCmd = 0x73;
        private const byte ReadProtectCmd = 0x82;
        private const byte ReadUnProtectCmd = 0x92;

        private int flashSize = 0;

        public STM32Programmer(int tryTime = 400)
        {
            BaudRate = 115200;
            ParityBit = Parity.Even;
            DataBit = 8;
            StopBit = StopBits.One;
            _tryTime = tryTime;
        }

        public override async Task<bool> Probe()
        {
            byte[] probeByte = { ProbeCmd } ;
            var ret = false;
            AppendLog("开始连接...");
            var StartTime = DateTime.Now;
            for (var i = 0; i < _tryTime; i++)
            {
                ProgressReport("连接芯片中...", i, _tryTime);
                Inst.Write(probeByte, 0, 1);
                if (await WaitAck())
                {
                    AppendLog($"连接成功，耗时{(DateTime.Now - StartTime).TotalMilliseconds}毫秒");
                    ret = true;
                    break;
                }

                await Task.Delay(1000);
            }
            ProgressReport("连接芯片成功", 0, 1);
            return ret;
        }

        public override async Task<bool> Clear()
        {
            var ret = false;
            try
            {
                do
                {
                    var StartTime = DateTime.Now;
                    if (!await SendCommand(EraseCmd))
                    {
                        break;
                    }

                    byte[] erGlobal = { 0xff, 0x00 };
                    Inst.Write(erGlobal, 0, 2);
                    if (await WaitAck(5000))
                    {
                        AppendLog($"清除芯片成功，耗时{(DateTime.Now - StartTime).TotalMilliseconds}毫秒");
                    }
                    else
                    {
                        break;
                    }
                    ret = true;
                } while (false);
            }
            catch (Exception)
            {
                return false;
            }
            return ret;
        }

        private HexFile _reader = new HexFile();

        public override async Task<bool> Flash(string fn)
        {
            var ret = false;
            try
            {
                if (!_reader.Parse(fn))
                {
                    AppendLog("解析程序文件错误");
                    return false;
                }

                var size = _reader.Blocks.Sum(block => block.Length);
                AppendLog($"程序大小: {size} ({size / 1024.0:F2}KB)");
                var StartTime = DateTime.Now;
                var written = 0;
                ProgressReport("下载中...", written, (int)size);
                foreach (var block in _reader.Blocks)
                {
                    if (!await WriteMemory(block.StartAddr, block.Content.ToArray()))
                    {
                        throw new Exception($"写入0x{block.StartAddr:X8} ({block.Length}字节) 出错");
                    }

                    written += (int)block.Length;
                    ProgressReport("下载中...", written, (int)size);
                }
                var elpase = (DateTime.Now - StartTime).TotalSeconds;
                AppendLog($"下载成功，耗时{elpase:F2}秒，下载速率{size / elpase / 1024:F2}KB/S");
                ret = true;
                ProgressReport("下载完成", 0, 1);
            }
            catch (Exception e)
            {
                AppendLog($"下载程序失败，{e.Message}");
            }
            return ret;
        }

        public override async Task<bool> Run()
        {
            var ret = false;
            do
            {
                if (!await SendCommand(GoCmd))
                {
                    break;
                }
                var addrB = BitConverter.GetBytes(_reader.EntryPoint);
                byte chkSum = 0;
                byte[] req = new byte[5];
                for (var i = 0; i < 4; i++)
                {
                    chkSum ^= addrB[i];
                    req[3 - i] = addrB[i];
                }

                req[4] = chkSum;
                Inst.Write(req, 0, 5);
                if (!await WaitAck())
                {
                    break;
                }

                AppendLog("自动执行成功");
                ret = true;
            } while (false);
            return ret;
        }

        public override async Task<bool> GetMcuInfo()
        {
            var ret = false;
            do
            {
                var result = await ExecCommand(GetCmd, -1);
                if (!result.Item1)
                {
                    break;
                }

                AppendLog($"芯片内BootLoader版本号：{result.Item2[0] >> 4}.{result.Item2[0] & 0xf}");
                result = await ExecCommand(GetIdCmd, -1);
                if (!result.Item1)
                {
                    break;
                }

                var resp = result.Item2;
                Array.Reverse(resp);
                var pid = (int) BitConverter.ToInt16(resp, 0);
                AppendLog($"芯片内PID：{pid:X4} {GetDevNameByPID(pid)}");
                result = await ReadMemory(0x1FFFF7E0, 5 * 4);
                if (!result.Item1)
                {
                    break;
                }

                resp = result.Item2;

                flashSize = ((int) BitConverter.ToInt16(resp, 0)) * 1024;
                AppendLog($"UID: " +
                          $"{BitConverter.ToInt32(resp, 8):X8}-" +
                          $"{BitConverter.ToInt32(resp, 12):X8}-" +
                          $"{BitConverter.ToInt32(resp, 16):X8}, " +
                          $"Flash大小 {BitConverter.ToInt16(resp, 0)}KB");
                ret = true;
            } while (false);
            return ret;
        }

        private static Dictionary<int, string> _deviceTable = new Dictionary<int, string>
        {
            /* F0 */
	        {0x440, "STM32F030x8/F05xxx"              },
            {0x444, "STM32F03xx4/6"                   },
            {0x442, "STM32F030xC/F09xxx"              },
            {0x445, "STM32F04xxx/F070x6"              },
            {0x448, "STM32F070xB/F071xx/F72xx"        },
	        /* F1 */
	        {0x412, "STM32F10xxx Low-density"         },
            {0x410, "STM32F10xxx Medium-density"      },
            {0x414, "STM32F10xxx High-density"        },
            {0x420, "STM32F10xxx Medium-density VL"   },
            {0x428, "STM32F10xxx High-density VL"     },
            {0x418, "STM32F105xx/F107xx"              },
            {0x430, "STM32F10xxx XL-density"          },
	        /* F2 */
	        {0x411, "STM32F2xxxx"                     },
	        /* F3 */
	        {0x432, "STM32F373xx/F378xx"              },
            {0x422, "STM32F302xB(C)/F303xB(C)/F358xx" },
            {0x439, "STM32F301xx/F302x4(6/8)/F318xx"  },
            {0x438, "STM32F303x4(6/8)/F334xx/F328xx"  },
            {0x446, "STM32F302xD(E)/F303xD(E)/F398xx" },
	        /* F4 */
	        {0x413, "STM32F40xxx/41xxx"               },
            {0x419, "STM32F42xxx/43xxx"               },
            {0x423, "STM32F401xB(C)"                  },
            {0x433, "STM32F401xD(E)"                  },
            {0x458, "STM32F410xx"                     },
            {0x431, "STM32F411xx"                     },
            {0x441, "STM32F412xx"                     },
            {0x421, "STM32F446xx"                     },
            {0x434, "STM32F469xx/479xx"               },
            {0x463, "STM32F413xx/423xx"               },
	        /* G0 */
	        {0x460, "STM32G07xxx/08xxx"               },
	        /* F7 */
	        {0x452, "STM32F72xxx/73xxx"               },
            {0x449, "STM32F74xxx/75xxx"               },
            {0x451, "STM32F76xxx/77xxx"               },
	        /* H7 */
	        {0x450, "STM32H74xxx/75xxx"               },
	        /* L0 */
	        {0x457, "STM32L01xxx/02xxx"               },
            {0x425, "STM32L031xx/041xx"               },
            {0x417, "STM32L05xxx/06xxx"               },
            {0x447, "STM32L07xxx/08xxx"               },
	        /* L1 */
	        {0x416, "STM32L1xxx6(8/B)"                },
            {0x429, "STM32L1xxx6(8/B)A"               },
            {0x427, "STM32L1xxxC"                     },
            {0x436, "STM32L1xxxD"                     },
            {0x437, "STM32L1xxxE"                     },
	        /* L4 */
	        {0x435, "STM32L43xxx/44xxx"               },
            {0x462, "STM32L45xxx/46xxx"               },
            {0x415, "STM32L47xxx/48xxx"               },
            {0x461, "STM32L496xx/4A6xx"               },
	        /* These are not (yet) in AN2606: */
	        {0x641, "Medium_Density PL"               },
            {0x9a8, "STM32W-128K"                     },
            {0x9b0, "STM32W-256K"                     },
        };

        private static string GetDevNameByPID(int pid)
        {
            try
            {
                return _deviceTable[pid];
            }
            catch (Exception)
            {
                return "未知设备";
            }
        }

        public override async Task<bool> Check()
        {
            var ret = true;
            var StartTime = DateTime.Now;
            var size = _reader.Blocks.Sum(v => v.Length);
            var check = 0;
            ProgressReport("校验中...", check, (int)size);
            foreach (var block in _reader.Blocks)
            {
                var res = await ReadMemory(block.StartAddr, (int)block.Length);
                if (!res.Item1)
                {
                    ret = false;
                    break;
                }

                if (!res.Item2.SequenceEqual(block.Content))
                {
                    ret = false;
                    break;
                }

                check += (int) block.Length;
                ProgressReport("校验中...", check, (int)size);
            }

            if (ret)
            {
                AppendLog($"校验成功，耗时{(DateTime.Now - StartTime).TotalSeconds:F2}秒");
                ProgressReport("校验完成", 0, 1);
            }
            return ret;
        }

        // 执行命令
        private async Task<Tuple<bool, byte[]>> ExecCommand(byte cmd, int respLen)
        {
            var ret = false;
            byte[] resp = null;
            try
            {
                if (!await SendCommand(cmd))
                {
                    return Tuple.Create(ret, resp);
                }

                if (respLen == -1)
                {
                    respLen = ReadCount();
                }

                if (respLen == 0)
                {
                    if (await WaitAck())
                    {
                        ret = true;
                    }
                }
                else if (respLen > 0)
                {
                    var res = await ReadResponse(respLen);
                    if (res.Item1 > 0)
                    {
                        ret = true;
                        resp = res.Item2;
                    }
                }

                return Tuple.Create(ret, resp);
            }
            catch (Exception)
            {
                return Tuple.Create(ret, resp);
            }
        }

        // 读取内存
        private async Task<Tuple<bool, byte[]>> ReadMemory(UInt32 addr, int cnt)
        {
            byte[] mem = null;
            if (cnt > 256)
            {
                return Tuple.Create(false, mem);
            }

            try
            {
                if (!await SendCommand(ReadCmd))
                {
                    return Tuple.Create(false, mem);
                }

                var addrB = BitConverter.GetBytes(addr);
                byte chkSum = 0;
                byte[] req = new byte[5];
                for (var i = 0; i < 4; i++)
                {
                    chkSum ^= addrB[i];
                    req[3 - i] = addrB[i];
                }

                req[4] = chkSum;
                Inst.Write(req, 0, 5);
                if (!await WaitAck())
                {
                    return Tuple.Create(false, mem);
                }
                req[0] = (byte) (cnt - 1);
                req[1] = (byte)(req[0] ^ 0xff);
                Inst.Write(req, 0, 2);
                if (!await WaitAck())
                {
                    return Tuple.Create(false, mem);
                }

                mem = new byte[cnt];
                var idx = 0;
                while (cnt > idx)
                {
                    var readCnt = await Inst.BaseStream.ReadAsync(mem, idx, cnt - idx);
                    if (readCnt == 0)
                    {
                        return Tuple.Create(false, mem);
                    }

                    idx += readCnt;
                }

                return Tuple.Create(true, mem);
            }
            catch (Exception)
            {
                return Tuple.Create(false, mem);
            }
        }

        // 写入内存
        private async Task<bool> WriteMemory(UInt32 addr, byte[] mem)
        {
            if (mem.Length > 256)
            {
                return false;
            }

            try
            {
                if (!await SendCommand(WriteCmd))
                {
                    return false;
                }

                var addrB = BitConverter.GetBytes(addr);
                byte chkSum = 0;
                byte[] req = new byte[5];
                for (var i = 0; i < 4; i++)
                {
                    chkSum ^= addrB[i];
                    req[3 - i] = addrB[i];
                }

                req[4] = chkSum;
                Inst.Write(req, 0, 5);
                if (!await WaitAck())
                {
                    return false;
                }
                req = new byte[(mem.Length + 3) / 4 * 4 + 2];
                req[0] = (byte)((mem.Length + 3) / 4 * 4 - 1);
                Array.Copy(mem, 0, req, 1, mem.Length);
                chkSum = 0;
                for (var i = 0; i < req.Length - 1; i++)
                {
                    chkSum ^= req[i];
                }

                req[req.Length - 1] = chkSum;
                Inst.Write(req, 0, req.Length);
                if (!await WaitAck())
                {
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 等待ACK/NACK
        private async Task<bool> WaitAck(int timeout = 1000)
        {
            
            var ret = false;
            try
            {
                int token = 0;
                await Task.Run(() =>
                {
                    Inst.ReadTimeout = timeout;
                    token = Inst.ReadByte();
                });
                if (token == ACK)
                {
                    ret = true;
                }
            }
            catch (Exception)
            {
                // ignore
            }
            return ret;
        }

        private void DummyEventHandler(object sender, SerialDataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        // 发送命令并等待响应
        private async Task<bool> SendCommand(byte cmd)
        {
            byte[] req = {cmd, (byte)(~cmd)};
            Inst.Write(req, 0, 2);

            return await WaitAck();
        }

        // 读取字节数
        private int ReadCount(int timeout = 1000)
        {
            int cnt = 0;
            try
            {
                cnt = Inst.ReadByte() + 1;
                return cnt;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 读取结果
        private async Task<Tuple<int, byte[]>> ReadResponse(int cnt, bool hasAck = true, int timeout = 10000)
        {
            byte[] resp = null;
            if (cnt <= 0)
            {
                return Tuple.Create(0, resp);
            }
            try
            {
                resp = new byte[cnt];
                var idx = 0;
                while (cnt > idx)
                {
                    var readCnt = await Inst.BaseStream.ReadAsync(resp, idx, cnt - idx);
                    if (readCnt == 0)
                    {
                        return Tuple.Create(-1, resp);
                    }

                    idx += readCnt;
                }
                if (await WaitAck())
                {
                    return Tuple.Create(cnt, resp);
                }
                else
                {
                    return Tuple.Create(-1, resp);
                }
            }
            catch (Exception)
            {
                return Tuple.Create(-1, resp);
            }
        }
    }
}
