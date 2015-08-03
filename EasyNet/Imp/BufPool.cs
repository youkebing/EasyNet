using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNet.Imp {
    internal class BufPool: SimpleObjPool<byte[]> {
        readonly int _bufsize;
        protected override byte[] NewObj() {
            return new byte[_bufsize];
        }
        public BufPool(int Len, int bufSize) : base(Len) {
            _bufsize = bufSize;
        }
    }
}
