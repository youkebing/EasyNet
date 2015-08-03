using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace EasyNet.Imp {
    class SocketHelper {
        public static void FreeSocket(Socket socket) {
            if (socket == null)
                return;
            try {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch {
            }
            try {
                socket.Close();
            }
            catch {
            }
            try {
                socket.Dispose();
            }
            catch {
            }
        }
    }
}
