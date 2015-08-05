using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using EasyNet.Base;

namespace EasyNet.Imp {
    public class EasyClient {
        Dictionary<string, Action<byte[], Action<byte[], Exception>>> _handes = new Dictionary<string, Action<byte[], Action<byte[], Exception>>>(StringComparer.CurrentCultureIgnoreCase);
        Dictionary<string, Action<byte[]>> _subhandes = new Dictionary<string, Action<byte[]>>(StringComparer.CurrentCultureIgnoreCase);
        Dictionary<int, Action<byte[], Exception>> _rpchandels = new Dictionary<int, Action<byte[], Exception>>();
        public void RegSub(string k, Action<byte[]> on) {
            _sch.Post(() => {
                _subhandes[k] = on;
                _syncsubsflag  = true;
                _sch.Post(SyncSubs);
            });
        }
        public void UnRegSub(string k) {
            _sch.Post(() => {
                if (_subhandes.Remove(k)) {
                    _syncsubsflag = true;
                    _sch.Post(SyncSubs);
                }
            });
        }
        static int id = 0;
        int GetNewID() {
            return Interlocked.Increment(ref id);
        }
        //static Exception NoActiveException = new Exception("no connect to apigateway");
        public void Pub(string k, bool brocast, byte[] buf) {
            _sch.Post(() => {
                InitAdapter();
                if (!Active) {
                    return;
                }
                try {
                    var ms = PktHelper.NewPkt(PktType.PubData);
                    ms.WriteByte((byte)(brocast? 1: 0)); //广播
                    ms.WriteString(k);
                    ms.WriteBytes(buf, 0, buf.Length);
                    PktHelper.ClosePkt(ms);
                    buf = ms.ToArray();
                    _Ada.Write(buf, 0, buf.Length, null);
                }
                catch {
                }
            });
        }
        public void Ping() {
            _sch.Post(_Ping);
        }
        public Action OnPongMsg { get; set; }
        void _Ping() {
            var ms = PktHelper.NewPkt(PktType.Ping);
            PktHelper.ClosePkt(ms);;
            var buf = ms.ToArray();
            _Ada.Write(buf, 0, buf.Length, null);
        }
        public void Rpc(string k, int dly, byte[] buf, Action<byte[], Exception> on) {
            _sch.Post(() => {
                _Rpc(k, dly, buf, on);
            });
        }
        void _Rpc(string k, int dly, byte[] buf, Action<byte[], Exception> on) {
            if (!Active) {
                on(null, new Exception("connect err"));
                return;
            }   
            int id = GetNewID();
            try {
                var ms = PktHelper.NewPkt(PktType.RpcRequest);
                ms.WriteInt(id);
                ms.WriteInt(0);
                ms.WriteInt(0);
                ms.WriteString(k);
                ms.WriteBytes(buf, 0, buf.Length);
                PktHelper.ClosePkt(ms);
                buf = ms.ToArray();
                _Ada.Write(buf, 0, buf.Length, null);
                if (on == null) {
                    return;
                }
                _rpchandels[id] = on;
            }
            catch {
                _rpchandels.Remove(id);
                on(null, new Exception("connect err"));
                return;
            }
            _timer.DlyExcute(dly, () => {
                if (_rpchandels.Remove(id)) {
                    on(null, new Exception("timer out"));
                }
            });
        }
        public void RegHandle(string k, Action<byte[], Action<byte[], Exception>> handle) {
            _sch.Post(() => {
                _handes[k] = handle;
                _syncf = true;
                _sch.Post(SyncHandles);
            });
        }
        public void UnRegHandle(string k) {
            _sch.Post(() => {
                if (_handes.Remove(k)) {
                     _syncf = true;
                    _sch.Post(SyncHandles);
                }
            });
        }
        bool _syncf = true;
        void SyncHandles() {
            if (!_syncf) {
                return;
            }
            _syncf = false;
            var ms = PktHelper.NewPkt(PktType.RpcHandles);
            ms.WriteStrs(_handes.Keys.ToArray());
            PktHelper.ClosePkt(ms);
            var buf = ms.ToArray();
            _Ada.Write(buf, 0, buf.Length, null);
        }
        bool _syncsubsflag = true;
        void SyncSubs() {
            if (!_syncsubsflag) {
                return;
            }
            _syncsubsflag = false;
            var ms = PktHelper.NewPkt(PktType.TopicHandles);
            ms.WriteStrs(_subhandes.Keys.ToArray());
            PktHelper.ClosePkt(ms);
            var buf = ms.ToArray();
            _Ada.Write(buf, 0, buf.Length, null);
        }
        static Sch _sch = new Sch(1);
        SimpleTcpAdapter _Ada = null;
        MemoryStream ms = new MemoryStream();
        IPEndPoint _ep;
        void InitAdapter() {
            SimpleTcpAdapter ada = Interlocked.CompareExchange(ref _Ada, null, null);
            if (ada != null) {
                if (!ada.Closed) {
                    return;   
                }
                else {
                    ada.Free();
                    ada = null;
                }
            }  
            Socket sc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try {
                sc.Connect(_ep);
                ms = new MemoryStream();
                ada = new SimpleTcpAdapter(sc);
            }
            catch(Exception ee) {
                SocketHelper.FreeSocket(sc);
                Console.WriteLine("socket ctor__" + ee.Message);
            }
            try {
                ada.OnData = OnRead;
                ada.Start();
                var v = Interlocked.Exchange(ref _Ada, ada);
                if (v != null) {
                    v.Free();
                    v.OnData = null;
                }
                _timer.DlyExcute(5, () => {
                    _sch.Post(SyncHandles);
                    _sch.Post(SyncSubs);
                });
                _sch.Post(SyncHandles);
                _sch.Post(SyncSubs);
            }
            catch {
                if (ada != null) {
                    ada.Free();
                    ada.OnData = null;
                }
            }
        }
        internal static readonly SimpleTimer _timer;
        static EasyClient() {
            _timer = new SimpleTimer(_sch);
        }
        public EasyClient(IPEndPoint ep, string name) {
            _ep = ep;
            Name = name;              
            _sch.Post(InitPktCall);
        }
        public void SetEp(IPEndPoint ep) {
            if (ep.Equals(_ep)) {
                return;
            }
            Stop(null);
            _ep = ep;
        }
        public void Start(Action on) {
            _sch.Post(InitAdapter);
            _sch.Post(on);
        }
        public void Stop(Action on) {
            SimpleTcpAdapter ada = Interlocked.Exchange(ref _Ada, null);
            if (ada == null) {
                return;
            }
            ada.Free();
            _sch.Post(on);
        }
        void OnRead(byte[] buff, int offset, int length) {
            _sch.Post(() => { _OnRead(buff, offset, length); });
        }
        static BufRead _bufhelper = new BufRead();
        void OnPkt(PktType pt, byte[] buf, int offset, int length) {  
            _bufhelper.Init(buf, offset, length);
            int ii = (int)pt;
            if ((ii >= 0) && (ii < _calls.Length)) {
                var f = _calls[ii];
                if (f != null) {
                    f(buf, offset, length);
                }
            }
        }
        void _OnRead(byte[] buf, int offset, int length) {
            try {
                ms.Position = ms.Length;
                ms.Write(buf, offset, length);
                PoolManage.FreeBuf(buf);
                PktHelper.ParsePkts(ms, OnPkt);
            }
            catch {
                _Ada.Free();
            }
        }
        public void Dispose() {
            _Ada.Dispose();
        }

        public string Name {
            get;
            private set;
        }

        public bool Active {
            get {
                var a = _Ada;
                if (a == null) {
                    return false;
                }
                return !a.Closed;
            }
            set {
                if (value == Active) {
                    return;
                }
                if (value) {
                    Start(null);
                }
                else {
                    Stop(null);
                }
            }
        }
        void OnErrClose(byte[] buf, int offset, int length) {
            _Ada.Free();
        }
        void OnRpcHandles(byte[] buf, int offset, int length) {
        }
        void OnTopicHandles(byte[] buf, int offset, int length) {
        }
        void OnRpcRequest(byte[] buf, int offset, int length) {
            int msgid = _bufhelper.ReadInt();
            int source = _bufhelper.ReadInt();
            int destination = _bufhelper.ReadInt();
            string k = _bufhelper.ReadString();
            Action<byte[], Action<byte[], Exception>> call = null;
            if (_handes.TryGetValue(k, out call)) {
                var es = _bufhelper.ReadBytes();
                Action<byte[], Exception> end = (b, e) => {
                    PktType pt = (e == null) ? PktType.RpcResponse : PktType.RpcErr;
                    var ms = PktHelper.NewPkt(pt);
                    ms.WriteInt(msgid);
                    ms.WriteInt(destination);
                    ms.WriteInt(source);
                    if (e != null) {
                        ms.WriteString(e.Message);
                    }
                    else {
                        ms.WriteBytes(b);
                    }
                    PktHelper.ClosePkt(ms);
                    buf = ms.ToArray();
                    _Ada.Write(buf, 0, buf.Length, null);
                };
                call(es, end);
            }
        }
        void OnRpcResponse(byte[] buf, int offset, int length) {
            int msgid = _bufhelper.ReadInt();
            int source = _bufhelper.ReadInt();
            int destination = _bufhelper.ReadInt();
            Action<byte[], Exception> on;
            if (_rpchandels.TryGetValue(msgid, out on)) { 
                _rpchandels.Remove(msgid);
                var es = _bufhelper.ReadBytes();
                on(es, null);
            }
        }
        void OnPubData(byte[] buf, int offset, int length) {
            _bufhelper.ReadByte();
            string k = _bufhelper.ReadString();
            Action<byte[]> call = null;
            if (_subhandes.TryGetValue(k, out call)) {
                var es = _bufhelper.ReadBytes();
                call(es);
            }
        }
        void OnRpcErr(byte[] buf, int offset, int length) {
            int msgid = _bufhelper.ReadInt();
            int source = _bufhelper.ReadInt();
            int destination = _bufhelper.ReadInt();
            Action<byte[], Exception> on;
            if (_rpchandels.TryGetValue(msgid, out on)) {
                _rpchandels.Remove(msgid);
                var es = _bufhelper.ReadString();
                on(null, new Exception(es));
            }
        }
        void OnPong(byte[] buf, int offset, int length) {
            var on = OnPongMsg;
            if (on == null) {
                return;
            }
            try {
                on();
            }
            catch {
            }
        }
        Action<byte[], int, int>[] _calls;
        void InitPktCall() {
            _calls = new Action<byte[], int, int>[(int)PktType.MAXLEN];
            _calls[(int)PktType.ErrClose] = OnErrClose;                //ErrClose = 1,
            _calls[(int)PktType.RpcHandles] = OnRpcHandles;            //SyncHandles = 4,
            _calls[(int)PktType.TopicHandles] = OnTopicHandles;        //SyncSubs = 7,
            _calls[(int)PktType.RpcRequest] = OnRpcRequest;            //RpcRequest = 8,
            _calls[(int)PktType.RpcResponse] = OnRpcResponse;          //RpcResponse = 9,
            _calls[(int)PktType.PubData] = OnPubData;                  //PubData = 10,
            _calls[(int)PktType.RpcErr] = OnRpcErr;                    //RpcErr = 11,
            _calls[(int)PktType.Pong] = OnPong;                        //OnPong = 11,
        }
    }
}
