using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace EasyNet.Base {
    public class Sch {
        public Sch(int cnt) {
            _cnt = (cnt < 1) ? 1 : cnt;
            if (_cnt == 1) {
                _Excute = _exc1;
            }
            else {
                _Excute = _exc;
            }
        }
        readonly int _cnt;
        int _cc = 0;
        LinkedList<Action> _lst = new LinkedList<Action>();
        //Queue<Action> _lst = new Queue<Action>();
        WaitCallback _Excute;
        public void Post(Action o) {
            if (o == null) {
                return;
            }
            lock (_lst) {
                _lst.AddLast(o);
                if (_cc >= _cnt) {
                    return;
                }
                _cc++;
            }
            ThreadPool.UnsafeQueueUserWorkItem(_Excute, null);
        }
        void _exc1(object o) {
            Action[] aa;
            while (true) {
                lock (_lst) {
                    if (_lst.Count == 0) {
                        _cc--;
                        return;
                    }
                    aa = _lst.ToArray();
                    _lst.Clear();
                }
                foreach (var v in aa) {
                    try {
                        v();
                    }
                    catch {
                    }
                }
            }
        }
        void _exc(object o) {
            Action aa;
            while (true) {
                lock (_lst) {
                    if (_lst.Count == 0) {
                        _cc--;
                        return;
                    }
                    aa = _lst.First.Value;
                    _lst.RemoveFirst();
                }
                try {
                    aa();
                }
                catch {
                }
            }
        }
    }
}
