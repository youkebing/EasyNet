using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace EasyNet.Base {
    public interface ICanCancel {
        void Cancel();
    }
    public class SimpleTimer : IDisposable {
        private readonly Timer _timer;
        private readonly Sch _sch;
        private readonly MinHeap<A> _minheap;
        private readonly AComparer _comparer;
        private int _cntid = 0;

        private const int maxDly = 1000 * 60 + 60;
        private const int repDly = 1000 * 60;
        private const int minDly = 5;

        class A : ICanCancel {
            public int cnt;
            public int tick;
            public Action Run;

            public SimpleTimer _simplertimer;
            public void Cancel() { 
                _Excute();
                var v = Interlocked.Exchange(ref _simplertimer, null);
                if (v != null) {
                    v.RemoveA(this);
                }
            }
            public void OnTime() {
                Interlocked.Exchange(ref _simplertimer, null);
                _Excute();
            }
            void _Excute() {
                if (Run != null) {
                    try {
                        Run();
                    }
                    catch {
                    }
                    Run = null;
                }
            }
           internal  Action<A> TTT;
           internal void AddA() {
               TTT(this);
               TTT = null;
           }
        }
        void RemoveA(A a) {
            _sch.Post(() => {
                _minheap.Remove(a);
                a.Run = null;
            });
        }
        class AComparer : IComparer<A> {
            public int Compare(A x, A y) {
                int i = x.tick - y.tick;
                if (i == 0) {
                    return x.cnt - y.cnt;
                }
                return i;
            }
        }
        public SimpleTimer(Sch sch) {
            _sch = sch;
            _timer = new Timer(OnTime, null, repDly, repDly);
            _comparer = new AComparer();
            _minheap = new MinHeap<A>(_comparer);
        }
        void AddA(A aa) {
            _minheap.Enqueue(aa);
            var a2 = _minheap.Peek();
            if (a2 == aa) {
                OnTimerSch();
            }
        }
        public ICanCancel DlyExcute(int dly, Action a) {
            dly = Math.Max(0, dly);
            int t = Environment.TickCount + Math.Min(dly, maxDly);
            int c = Interlocked.Increment(ref _cntid);
            var aa = new A { tick = t, Run = a, cnt = c, _simplertimer = this };

                aa.TTT = AddA;
                _sch.Post(aa.AddA);
            
            return aa;
        }
        void OnTimerSch() {
            int tick = Environment.TickCount;
            while (!_minheap.IsEmpty) {
                A a = _minheap.Peek();
                int dly = tick - a.tick;
                if (dly >= 0) {
                    _minheap.Dequeue();
                    Interlocked.Exchange(ref a._simplertimer, null);
                    a.OnTime();
                    continue;
                }
                dly = Math.Max(minDly, dly);
                _timer.Change(dly, repDly);
                return;
            }
        }

        void OnTime(object o) {
            _sch.Post(OnTimerSch);
        }

        public void Dispose() {
            try {
                _timer.Dispose();
                _minheap.Clear();
            }
            catch {
            }
        }
    }
}
