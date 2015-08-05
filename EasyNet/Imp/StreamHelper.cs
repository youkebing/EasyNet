using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
 using System.IO;

namespace EasyNet.Imp {
    static class StreamHelper {
        public static int GetShort(this byte[] buf, ref int offset) {
            int l = buf[offset];
            l <<= 8;
            l |= buf[offset + 1];
            offset += 2;
            return l;
        }
        public static int GetInt(this byte[] buf, ref int offset) {
            int l = buf[offset];
            l <<= 8;
            l |= buf[offset + 1];
            l <<= 8;
            l |= buf[offset + 2];
            l <<= 8;
            l |= buf[offset + 3];
            offset += 4;
            return l;
        }
        public static void WriteShort(this MemoryStream ms, int i) {
            byte[] buf = new byte[2];
            buf[1] = (byte)i;
            i >>= 8;
            buf[0] = (byte)i;
            ms.Write(buf, 0, 2);
        }
        public static void WriteInt(this MemoryStream ms, int i) {
            byte[] buf = new byte[4];
            buf[3] = (byte)i;
            i >>= 8;
            buf[2] = (byte)i;
            i >>= 8;
            buf[1] = (byte)i;
            i >>= 8;
            buf[0] = (byte)i;
            ms.Write(buf, 0, 4);
        }
        
        public static void WriteBytes(this MemoryStream ms, byte[] buf) {
            if (buf == null) {
                WriteInt(ms, -1);
                return ;
            }
            WriteInt(ms, buf.Length);
            ms.Write(buf, 0, buf.Length);
        }

        public static void WriteBytes(this MemoryStream ms, byte[] buf, int offset, int length) {
            if (buf == null) {
                WriteInt(ms, -1);
                return;
            }
            WriteInt(ms, length);
            ms.Write(buf, offset, length);
        }
        
        public static void WriteString(this MemoryStream ms, string s) {
            byte[] buf = null;
            if (s != null) {
                buf = Encoding.UTF8.GetBytes(s);
            }
            WriteBytes(ms, buf);
        }
        public static void WriteStrs(this MemoryStream ms, string[] ss) {
            if (ss == null) {
                WriteInt(ms, -1);
                return;
            }
            int a = ss.Length;
            WriteInt(ms, ss.Length);
            for (int i = 0; i < ss.Length; i++) {
                WriteString(ms, ss[i]);
            }
        }
    }
}
