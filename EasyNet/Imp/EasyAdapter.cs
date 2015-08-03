using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using EasyNet.Imp;

namespace EasyNet.Imp {
    class EasyAdapter : SimpleTcpAdapter {
        public EasyAdapter(EasyGateway br, Socket sc)
            : base(sc) {
            ID = br.NewID();
            broker = br;
            Start();
        }
        public int ID { get; private set; }
        public EasyGateway broker { get; private set; }
        MemoryStream _rdms = new MemoryStream();
        void OnPkt(PktType pt, byte[] buf, int offset, int length) {
            broker.OnPkt(this, pt, buf, offset, length);
        }
        protected override void OnRead(byte[] buf, int offset, int length) {
            broker.Excute(() => { 
                try {
                    _rdms.Position = _rdms.Length;
                    _rdms.Write(buf, offset, length);
                    PoolManage.FreeBuf(buf);
                    PktHelper.ParsePkts(_rdms, OnPkt);
                }
                catch(Exception ee) {
                    Free();
                }
            });
        }

        public string Dest { get; set; }

        public HashSet<string> Rpcs = new HashSet<string>(StringComparer.CurrentCulture);
        public HashSet<string> Subs = new HashSet<string>(StringComparer.CurrentCulture);

        internal  void WriteStream(MemoryStream ms) {
            Write(ms.ToArray(), 0, (int)ms.Length, null); 
        }
    }
}
