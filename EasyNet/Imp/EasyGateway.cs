using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using EasyNet.Base;

namespace EasyNet.Imp {
    public class EasyGateway {
        public readonly Sch Sch = new Sch(1);
        SimpleTcpListener _listen = new SimpleTcpListener();
        static int _adapterid;
        //对每个连接会自动生成一个唯一id
        Dictionary<int, EasyAdapter> _AllAdapters = new Dictionary<int, EasyAdapter>();
        //订阅的请求列表
        Dictionary<string, List<EasyAdapter>> _Subs = new Dictionary<string, List<EasyAdapter>>(StringComparer.CurrentCultureIgnoreCase);
        Dictionary<string, List<EasyAdapter>> _Rpcs = new Dictionary<string, List<EasyAdapter>>(StringComparer.CurrentCultureIgnoreCase);

        public int NewID() {
            while (true) {
                int ii = Interlocked.Increment(ref _adapterid);
                if (_AllAdapters.ContainsKey(ii)) {
                    continue;
                }
                if (ii == 0) {
                    continue;
                }
                return ii;
            }
        }
        int _poll = 0;
        void Poll() {
            Interlocked.Exchange(ref _poll, 1);
            Excute(_Poll);
        }
        
        static List<string> NullList = new List<string>();
        int _subchange = 0;
        void SubsChange() {
            _subchange = 1;
            Excute(_SubsChange);
        }
        void _SubsChange() {
            if (_subchange == 0) {
                return;
            }
            _subchange = 0;
            _Subs.Clear();
            List<EasyAdapter> lst;
            foreach (var v in _AllAdapters.Values) {
                foreach (var vv in v.Subs) {
                    _Subs.TryGetValue(vv, out lst);
                    if (lst == null) {
                        lst = new List<EasyAdapter>();
                        _Subs[vv] = lst;
                    }
                    lst.Add(v);
                }
            }
        }
        int _rpcchange = 0;
        void RpcsChange() {
            _rpcchange = 1;
            Excute(_RpcsChange);
        }
        void _RpcsChange() {
            if (_rpcchange == 0) {
                return;
            }
            _rpcchange = 0;
            _Rpcs.Clear();
            List<EasyAdapter> lst;
            foreach (var v in _AllAdapters.Values) {
                foreach (var vv in v.Rpcs) {
                    _Rpcs.TryGetValue(vv, out lst);
                    if (lst == null) {
                        lst = new List<EasyAdapter>();
                        _Rpcs[vv] = lst;
                    }
                    lst.Add(v);
                }
            }
        }

        static Random _random = new Random();  
        static BufRead _bufhelper = new BufRead();

        static bool HashChange(HashSet<string> o, HashSet<string> n) {
            if (o.Count == n.Count) {
                bool f = true;
                foreach (var v in n) {
                    if (o.Contains(v)) {
                        continue;
                    }  
                    f = false;
                }
                if (f) {
                    return false;
                }
            }
            o.Clear();
            foreach (var v in n) {
                o.Add(v);
            }
            return true;
        }

        internal void OnPkt(EasyAdapter adapter, PktType tp, byte[] buf, int offset, int length) { 
            _bufhelper.Init(buf, offset, length);

            int ii = (int)tp;
            if ((ii >= 0) && (ii < _calls.Length)) {
                var f = _calls[ii];
                if (f != null) {
                    f(adapter, buf, offset, length);
                }
            }
        }
        void _Poll() {
            if (Interlocked.Exchange(ref _poll, 0) == 0) {
                return;
            }
            bool b = false;
            foreach (var v in _AllAdapters.ToArray()) {
                var ag = v.Value;
                if ((ag == null) || (ag.Closed)) {
                    _AllAdapters.Remove(v.Key);
                    b = true;
                }
            }
            if (b) {
                SubsChange();
                RpcsChange();
            }
        }
        public EasyGateway() {
            _listen.OnNewAccept = OnNewSocket;
            Excute(InitPktCall);
        }

        public void SetEP(IPEndPoint ep) {
            if (ep.Equals(_listen.EP)) {
                return;
            }
            else {
                _listen.EP = ep;
                _listen.Stop();
            }
        }
        public void Start() {
            _listen.Start();
        }
        void OnNewSocket(Socket sc) {
            Excute(() => {
                EasyAdapter a = new EasyAdapter(this, sc);
                _AllAdapters[a.ID] = a;
                a.OnClose = this.Poll;
            });
        }
        public void Excute(Action exc) {
            Sch.Post(exc);
        }
        void OnRpcAck(PktType t, EasyAdapter adapter, byte[] buf, int offset, int length) {
            int msgid = _bufhelper.ReadInt();
            int source = _bufhelper.ReadInt();
            int destination = _bufhelper.ReadInt();
            EasyAdapter dest = null;
            _AllAdapters.TryGetValue(destination, out dest);
            if (adapter != null) {
                var ms = PktHelper.NewPkt(t);
                ms.Write(buf, offset, length);
                PktHelper.ClosePkt(ms);
                dest.WriteStream(ms);
            }
        }
        //PktType.RpcErr
        void OnRpcErr(EasyAdapter adapter, byte[] buf, int offset, int length) {
            OnRpcAck(PktType.RpcErr, adapter, buf, offset, length);
        }
        //PktType.RpcResponse
        void OnRpcResponse(EasyAdapter adapter, byte[] buf, int offset, int length) {
            OnRpcAck(PktType.RpcResponse, adapter, buf, offset, length);
        }
        //PktType.SyncHandles
        void OnRpcHandles(EasyAdapter adapter, byte[] buf, int offset, int length) {
            var hs = _bufhelper.ReadHashSet();
            if (HashChange(adapter.Rpcs, hs)) {
                RpcsChange();
            }
        }
        //PktType.SyncHandles
        void OnTopicHandles(EasyAdapter adapter, byte[] buf, int offset, int length) {
            var hs = _bufhelper.ReadHashSet();
            if (HashChange(adapter.Subs, hs)) {
                SubsChange();
            }
        }
        void OnRpcRequest(EasyAdapter adapter, byte[] buf, int offset, int length) {
            int msgid = _bufhelper.ReadInt();
            int source = _bufhelper.ReadInt();
            int destination = _bufhelper.ReadInt();
            EasyAdapter dest = null;
            var k = _bufhelper.ReadString();
            var lst = new List<EasyAdapter>();
            _Rpcs.TryGetValue(k, out lst);
            if (lst != null) {
                if (lst.Count == 1) {
                    dest = lst[0];
                }
                else if (lst.Count > 1) {
                    dest = lst[_random.Next(lst.Count)];
                }
            }
            if (dest == null) {
                var ms = PktHelper.NewPkt(PktType.RpcErr);
                ms.WriteInt(msgid);
                ms.WriteInt(adapter.ID);
                ms.WriteInt(adapter.ID);
                ms.WriteString("no such method!");
                //Console.WriteLine("no such method!");
                PktHelper.ClosePkt(ms);
                adapter.WriteStream(ms);
                //Console.WriteLine("no such method!----");
            }
            else {
                var ms = PktHelper.NewPkt(PktType.RpcRequest);
                ms.WriteInt(msgid);
                ms.WriteInt(adapter.ID);
                ms.WriteInt(dest.ID);
                //Console.WriteLine("api_" + dest.ID);
                ms.WriteString(k);
                _bufhelper.ReadBytes(out offset, out length);
                ms.WriteBytes(buf, offset, length);
                PktHelper.ClosePkt(ms);
                dest.WriteStream(ms);
            }
        }

        //PktType.SyncHandles
        void OnPubData(EasyAdapter adapter, byte[] buf, int offset, int length) {
            EasyAdapter dest = null;
            bool rp = _bufhelper.ReadByte() != 0;           //是否单播
            string k = _bufhelper.ReadString();
            List<EasyAdapter> lst;
            _Subs.TryGetValue(k, out lst);
            if ((lst == null) || (lst.Count == 0)) {
                return;
            }
            var ms = PktHelper.NewPkt(PktType.PubData);
            ms.Write(buf, offset, length);
            PktHelper.ClosePkt(ms);
            buf = ms.ToArray();
            length = (int)ms.Length;
            if (lst.Count == 1) {
                lst[0].Write(buf, 0, length, null);
            }
            else if (!rp) {
                dest = lst[_random.Next(lst.Count)];
                dest.Write(buf, 0, length, null);
            }
            else {
                foreach (var node in lst) {
                    node.Write(buf, 0, length, null);
                }
            }
        }
        //ErrClose
        void OnErrClose(EasyAdapter adapter, byte[] buf, int offset, int length) {
            adapter.Free();
        }
        void OnPing(EasyAdapter adapter, byte[] buf, int offset, int length) {
            var ms = PktHelper.NewPkt(PktType.Pong);
            PktHelper.ClosePkt(ms);
            adapter.WriteStream(ms);
        }
        Action<EasyAdapter, byte[], int, int>[] _calls;
        void InitPktCall() {
            _calls = new Action<EasyAdapter, byte[], int, int>[(int)PktType.MAXLEN];
            _calls[(int)PktType.ErrClose] = OnErrClose;                //ErrClose = 1,
            _calls[(int)PktType.RpcHandles] = OnRpcHandles;            //SyncHandles = 4,
            _calls[(int)PktType.TopicHandles] = OnTopicHandles;        //SyncSubs = 7,
            _calls[(int)PktType.RpcRequest] = OnRpcRequest;            //RpcRequest = 8,
            _calls[(int)PktType.RpcResponse] = OnRpcResponse;          //RpcResponse = 9,
            _calls[(int)PktType.PubData] = OnPubData;                  //PubData = 10,
            _calls[(int)PktType.RpcErr] = OnRpcErr;                    //RpcErr = 11,
            _calls[(int)PktType.Ping] = OnPing;                        //OnPing = 11,
        }
    }
}
