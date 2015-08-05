using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EasyNet.Imp {
    class PktHelper {
        static readonly int _Header = 0x1234;
        static readonly int HeadLength = 8;
        static bool ParseHeader(byte[] buf, int offset, int buflen, out int len, out PktType pt) {
            len = 0;
            pt = PktType.ErrClose;
            if (buflen - offset < HeadLength) {
                return false;
            }
            var h = buf.GetShort(ref offset);
            pt = (PktType)buf.GetShort(ref offset);
            len = buf.GetInt(ref offset);
            
            if (h != _Header) {
                pt = PktType.ErrClose;
                return true;
            }
            offset += len;
            if (offset > buflen) {
                return false;
            }
            
            return true;
        }
        internal static void ParsePkts(MemoryStream ms, Action<PktType, byte[], int, int> on) {
            var buf = ms.ToArray();
            int len = (int)ms.Length;
            int offset = 0;
            int ll;
            PktType pt;
            while (ParseHeader(buf, offset, len, out ll, out pt)) {
                offset += HeadLength;
                try {
                    on(pt, buf, offset, ll);
                }
                catch {
                }
                offset += ll;
            }
            if (offset == 0) {
                return;
            }
            try {
                ll = (int)ms.Length - offset;
                ms.SetLength(0);
                ms.Write(buf, offset, ll);
                int max = 1024 * 1024;
                if (ms.Capacity < max) {
                    return;
                }
                if ((int)ms.Length * 2 > max) {
                    return;
                }
                ll = Math.Max((int)ms.Length * 2, 1024 * 20);
                if (ms.Capacity > ll) {
                    ms.Capacity = ll;
                }
            }
            catch {
                throw;
            }
        }
        public static MemoryStream NewPkt(PktType p) {
            var ms = new MemoryStream();
            ms.WriteShort(_Header);
            ms.WriteShort((int)p);
            ms.WriteInt((int)ms.Length - HeadLength);
            return ms;
        }
        public static void ClosePkt(MemoryStream ms) {
            ms.Position = 4;
            ms.WriteInt((int)ms.Length - HeadLength);
        }
    }
}
