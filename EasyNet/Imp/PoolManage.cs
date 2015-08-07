using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace EasyNet.Imp {
    internal static class PoolManage {
        readonly static EvPool _evPool = new EvPool(100);
        readonly static BufPool _bufPool = new BufPool(2000, 1024 * 5);
        public static void FreeBuf(byte[] buf) {
            if ((buf == null) || (buf.Length != 1024 * 5)) {
                return;
            }
            _bufPool.FreeObj(buf);
        }
        public static byte[] GetBuf() {
            return _bufPool.GetObj();
        }
        public static void FreeScEv(SocketAsyncEventArgs buf) {
            if (_evPool == null) {
                return;
            }
            if (!_evPool.FreeObj(buf)) {
                buf.Dispose();
            }
        }
        public static SocketAsyncEventArgs GetScEv() {
            return _evPool.GetObj();
        }
    }
}
