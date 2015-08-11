using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace EasyNet.Imp {
    internal class EvPool: SimpleObjPool<SocketAsyncEventArgs> {
        protected override SocketAsyncEventArgs NewObj() {
            return new SocketAsyncEventArgs();
        }
        public EvPool(int Len)
            : base(Len) {
        }
        static byte[] _gdbuf = new byte[1];
        public override bool FreeObj(SocketAsyncEventArgs t) {
            t.SetBuffer(_gdbuf, 0, 1);
            return base.FreeObj(t);
        }
    }
}
