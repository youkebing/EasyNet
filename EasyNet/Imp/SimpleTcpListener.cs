using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyNet.Imp {
    public class SimpleTcpListener {
        Socket _sc = null;
        SocketAsyncEventArgs acceptEventArg = new SocketAsyncEventArgs();
        public SimpleTcpListener() {
            acceptEventArg.Completed += OnAcceptCompleted;
        }

        public void Stop() {
            var sc = Interlocked.Exchange<Socket>(ref _sc, null);
            if (sc == null) {
                return;
            }
            On(OnClose, sc);
            SocketHelper.FreeSocket(sc);
        }
        public IPEndPoint EP {
            get;
            set;
        }
        static void On(Action<Socket> on, Socket sc) {
            try {
                on(sc);
            }
            catch { 
            }
        }
        public void Start() {
            if (_sc != null) {
                return;
            }
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var sc = Interlocked.CompareExchange(ref _sc, socket, null);
            if (sc != null)
                return;
            try {
                _sc.Bind(EP);
                On(OnOpen, _sc);
                _sc.Listen(10);
                StartAccept();
            }
            catch {
                Stop();
            }
        }

        public Action<Socket> OnNewAccept { get; set; }
        public Action<Socket> OnOpen { get; set; }
        public Action<Socket> OnClose { get; set; }

        void StartAccept() {
            var sc = Interlocked.CompareExchange<Socket>(ref _sc, null, null);
            if (sc == null)
                return;
            try {
                acceptEventArg.AcceptSocket = null;
                if (!sc.AcceptAsync(acceptEventArg)) {
                    OnAcceptCompleted(this, acceptEventArg);
                }
            }
            catch {
                Stop();
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e) {
            Socket sc = Interlocked.CompareExchange<Socket>(ref _sc, null, null);
            if (sc == null)
                return;
            try {
                sc = acceptEventArg.AcceptSocket;
                acceptEventArg.AcceptSocket = null;
                if ((sc == null) || (sc.Connected == false) || (OnNewAccept == null)) {
                    SocketHelper.FreeSocket(sc);
                }
                else {
                    OnNewAccept(sc);
                }
            }
            catch {
            }
            StartAccept();
        }
    }
}
