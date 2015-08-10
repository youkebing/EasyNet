using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyNet.Base;

namespace EasyNet.Imp {
    public class SimpleTcpAdapter : IDisposable {
        SocketAsyncEventArgs _ReadEventArgs;
        SocketAsyncEventArgs _WriteEventArgs;

        static Sch _wsch = new Sch(1);
        MemoryStream _wms = new MemoryStream();
        bool _writeflag = false;

        void _WriteNext() {
            _writeflag = false;
            _Write();
        }

        void ZipStream(ref MemoryStream ms) {  
            if (ms.Position == 0) {
                return;
            }
            if (ms.Position < 1024 * 50) {
                return;
            }
            if (ms.Length == 0) {
                ms = new MemoryStream();
                return;
            }
            var dest = new MemoryStream();
            ms.CopyTo(dest);
            dest.Position = 0;
            ms = dest;
        }
        void _Write() {
            if (_writeflag) {
                return;
            }
            if (_wms == null) {
                return;
            }
            if ((_wms.Length - _wms.Position) <= 0) {
                return;
            }
            try {
                var buf = _WriteEventArgs.Buffer;
                var length = _wms.Read(buf, 0, buf.Length);
                ZipStream(ref _wms);
                _WriteEventArgs.SetBuffer(0, length);
                bool f = false;
                try {
                    f = _sc.SendAsync(_WriteEventArgs);
                }
                catch {
                    UnInitEventArgs(Interlocked.Exchange(ref _WriteEventArgs, null), OnWriteCompleted);
                    Free();
                    throw;
                }
                if (!f) {
                    try {
                        OnWriteCompleted(this, _WriteEventArgs);
                    }
                    catch {
                    };
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
                _writeflag = false;
                Free();
                return;
            }  
            _writeflag = true;
        }

        public void Write(byte[] buf, int offset, int length) {
            if (Closed) {
                return;
            }
            _wsch.Post(() => {
                if (_wms == null) {
                    return;
                }
                var p = _wms.Position;
                _wms.Position = _wms.Length;
                _wms.Write(buf, offset, length);
                _wms.Position = p;
                _Write();
            });
        }

        readonly int MaxLen;
        public Socket _sc = null;
        protected virtual void OnRead(byte[] buf, int offset, int length) {
            if (length <= 0) {
                return;
            }
            try {
                OnData(buf, offset, length);
            }
            catch {
            }
        }
        SocketAsyncEventArgs InitEventArgs(EventHandler<SocketAsyncEventArgs> evhandler) {
            SocketAsyncEventArgs e = PoolManage.GetScEv();
            var buf = PoolManage.GetBuf();
            e.SetBuffer(buf, 0, buf.Length);
            e.Completed += evhandler;
            return e;
        }
        void UnInitEventArgs(SocketAsyncEventArgs ev, EventHandler<SocketAsyncEventArgs> h) {
            if (ev == null) {
                return;
            }
            var buf = ev.Buffer;
            ev.Completed -= h;
            PoolManage.FreeBuf(buf);
            PoolManage.FreeScEv(ev);
        }
        public Action<byte[], int, int> OnData { get; set; }

        /*
        -----------------------------------------------------------------------------------------------------------------
          Socket与拔掉网线
        -----------------------------------------------------------------------------------------------------------------
        当客户端与服务端通过Tcp Socket进行通信时，如果客户端应用正常退出或异常退出，服务端都会在对应的连接上获取感知（如
        返回0、或抛出异常）。但是，如果客户端的网线被拔掉，那么，默认情况下，服务端需要在2个小时后才会感知客户端掉线。对
        于很多服务端应用程序来说，这么长的反应时间是不能忍受的。
        我们通常在应用层使用“心跳机制”来解决类似的问题，这是可行的。
        然而，在这里，我们可以使用Socket自己的心跳机制来解决这一问题。 
        System.Net.Sockets.Socket提供了IOControl（）方法给我们来设置Sokect的心跳机制的相关参数。比如，我们设置KeepAlive的
        时间为20秒，检查间隔为2秒。可以这样做：
        int keepAlive = -1744830460; // SIO_KEEPALIVE_VALS
        byte[] inValue = new byte[] { 1, 0, 0, 0, 0x20, 0x4e, 0, 0, 0xd0, 0x07, 0, 0 }; //True, 20 秒, 2 秒
        sock.IOControl(keepAlive, inValue, null);
        20秒（20000毫秒）的16进制表示是4e20，2秒（2000毫秒）的16进制表示是07d0，如此，你可以修改inValue参数为自己希望的值。
        在上述设置下，如果拨掉客户端网线，服务器Socket.Receive()会在20秒后抛出异常（注意，在这20秒服务端内无论是从该socket
        上接收消息还是发送消息都不会抛出异常！）。
        */
        static int keepAlive = -1744830460; // SIO_KEEPALIVE_VALS
        static byte[] inValue = new byte[] { 1, 0, 0, 0, 0x40, 0x9C, 0, 0, 0x58, 0x1B, 0, 0 }; //True, 20 秒, 2 秒
        public SimpleTcpAdapter(Socket sc) {  
            _sc = sc;
            _ReadEventArgs = InitEventArgs(OnReadCompleted);
            _WriteEventArgs = InitEventArgs(OnWriteCompleted);
            try {
                sc.IOControl(keepAlive, inValue, null);
            }
            catch {
            }
            MaxLen = _ReadEventArgs.Buffer.Length; 
        }

        int _flag = 1;
        public virtual void Start() {
            int f = Interlocked.Exchange(ref _flag, 0);
            if (f != 0) {
                Read();
            }
        }

        public bool Closed {
            get {
                return _sc == null;
            }
        }
        void OnReadCompleted(object sender, SocketAsyncEventArgs e) {
            try {
                int len = _ReadEventArgs.BytesTransferred;
                int offset = _ReadEventArgs.Offset;
                var buf = _ReadEventArgs.Buffer;
                if ((len <= 0) || (_ReadEventArgs.SocketError != SocketError.Success)) {
                    Free();
                    len = 0;
                    return;
                }
                if (len > 0) {
                    var b = PoolManage.GetBuf();
                    Buffer.BlockCopy(buf, offset, b, 0, len);
                    OnRead(b, 0, len);
                }
                Read();
            }
            catch {
                Free();
            }
        }
        void OnWriteCompleted(object sender, SocketAsyncEventArgs e) {
            try {
                int len = _WriteEventArgs.BytesTransferred;

                if ((len < 0) || (_WriteEventArgs.SocketError != SocketError.Success)) {
                    Free();
                    len = 0;
                }
            }
            catch {
            }
            _wsch.Post(_WriteNext);
        }

        #region Dispose
        public void Dispose() {
            Free();
            try {
                _ReadEventArgs.Dispose();
            }
            catch {
            }
        }
        #endregion

        public Action OnClose { get; set; }

        #region Free
        public void Free() {
            Socket sc = Interlocked.Exchange<Socket>(ref _sc, null);
            if (sc == null)
                return;
            _wms = null;
            SocketHelper.FreeSocket(sc);
            try {
                OnClose();
            }
            catch {
            }
        }
        #endregion

        void Read() {
            try {
                var buf = _ReadEventArgs.Buffer;
                _ReadEventArgs.SetBuffer(0, MaxLen);
                bool f = false;
                try {
                    f = _sc.ReceiveAsync(_ReadEventArgs);
                }
                catch {
                    UnInitEventArgs(Interlocked.Exchange(ref _ReadEventArgs, null), OnReadCompleted);
                    throw;
                }
                if (!f) {
                    OnReadCompleted(this, _ReadEventArgs);
                }
            }
            catch {
                Free();
            }
        }
    }
}
