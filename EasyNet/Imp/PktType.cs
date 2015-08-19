using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNet.Imp {
    internal enum PktType {
        ErrClose = 1,
        RpcHandles = 2,
        TopicHandles = 3,
        RpcRequest = 4,
        RpcResponse = 5,
        PubData = 6,
        RpcErr = 7,
        Ping = 8,
        Pong = 9,
        Open = 10,
        MAXLEN = 11
    }
}
