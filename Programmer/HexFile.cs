using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Annotations;
using Exception = System.Exception;

namespace STM32Programmer.Programmer
{
    class HexFile
    {
        public class MemoryBlock
        {
            public UInt32 StartAddr = 0;

            public UInt32 Length
            {
                get
                {
                    return (UInt32)Content.Count;
                }
            }
            public List<byte> Content = new List<byte>();
        }

        class HexLine
        {
            public enum HexLineType
            {
                Data,
                EndOfFile,
                ExtendedSegmentAddress,
                StartSegmentAddress,
                ExtendedLinearAddress,
                StartLinearAddress,
                HexLineTypeMax,
            }

            public HexLineType Type = HexLineType.Data;
            public UInt32 Addr = 0;
            public byte[] Content = null;

            public bool Parse(string line)
            {
                bool ret = false;
                do
                {
                    if (line[0] != ':')
                    {
                        break;
                    }

                    var chkSum = Enumerable.Range(1, line.Length - 1)
                        .Where(x => x % 2 == 1)
                        .Select(x => Convert.ToByte(line.Substring(x, 2), 16))
                        .Sum((b => b)) & 0xff;
                    if (chkSum != 0)
                    {
                        return false;
                    }

                    int len = int.Parse(line.Substring(1, 2), NumberStyles.HexNumber);
                    int type = int.Parse(line.Substring(7, 2), NumberStyles.HexNumber);
                    if (type >= (int)HexLineType.HexLineTypeMax)
                    {
                        break;
                    }

                    Type = (HexLineType) type;
                    Addr = UInt32.Parse(line.Substring(3, 4), NumberStyles.HexNumber);
                    if (Type == HexLineType.ExtendedLinearAddress)
                    {
                        Addr = UInt32.Parse(line.Substring(9, 4), NumberStyles.HexNumber);
                    }
                    else if (Type == HexLineType.StartLinearAddress)
                    {
                        Addr = UInt32.Parse(line.Substring(9, 8), NumberStyles.HexNumber);
                    }
                    else if (Type == HexLineType.Data)
                    {
                        Content = Enumerable.Range(9, len * 2)
                            .Where(x => x % 2 == 1)
                            .Select(x => Convert.ToByte(line.Substring(x, 2), 16))
                            .ToArray();
                    }
                    ret = true;
                } while (false);

                return ret;
            }
        }

        public List<MemoryBlock> Blocks = new List<MemoryBlock>();

        public UInt32 EntryPoint = 0;

        public bool Parse(string fn, int maxBlockSize = 256)
        {
            var ret = false;
            try
            {
                using (var reader = File.OpenText(fn))
                {
                    string line;
                    UInt32 addrPrefix = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        HexLine hexLine = new HexLine();
                        if (!hexLine.Parse(line))
                        {
                            return false;
                        }

                        if (hexLine.Type == HexLine.HexLineType.ExtendedLinearAddress)
                        {
                            addrPrefix = hexLine.Addr << 16;
                        }
                        else if (hexLine.Type == HexLine.HexLineType.StartLinearAddress)
                        {
                            EntryPoint = hexLine.Addr;
                        }
                        else if (hexLine.Type == HexLine.HexLineType.EndOfFile)
                        {
                            ret = true;
                            break;
                        }
                        else if (hexLine.Type == HexLine.HexLineType.Data)
                        {
                            var addr = addrPrefix | hexLine.Addr;
                            MemoryBlock block = null;
                            try
                            {
                                block = Blocks.Find(v =>
                                    v.StartAddr < addr && v.StartAddr + v.Length == addr && v.Length + hexLine.Content.Length <= maxBlockSize);
                            }
                            catch (Exception)
                            {
                                // ignore
                            }
                            if (block == null)
                            {
                                block = new MemoryBlock();
                                block.StartAddr = addr;
                                Blocks.Add(block);
                            }
                            block.Content.AddRange(hexLine.Content);
                        }
                    }
                }
            }
            catch (Exception )
            {
                // ignore
            }

            return ret;
        }
    }
}
