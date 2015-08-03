using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace EasyNet.Imp {
    public abstract class SimpleObjPool<T> {
        int _locker = 0;
        int _cnt = 0;
        T[] _caches;
        public SimpleObjPool(int MaxSize) {
            _caches = new T[MaxSize];
        }
        protected abstract T NewObj();
        public T GetObj() {
            int a = Interlocked.Exchange(ref _locker, 1);
            if (a == 0) {
                if (_cnt > 0) {
                    _cnt--;
                    T t = _caches[_cnt];
                    Interlocked.Exchange(ref _locker, 0);
                    return t;
                }
                Interlocked.Exchange(ref _locker, 0);
            }
            return NewObj();
        }
        public virtual bool FreeObj(T t) {
            int a = Interlocked.Exchange(ref _locker, 1);
            if (a == 0) {
                if (_cnt < _caches.Length) {
                    _caches[_cnt] = t;
                    _cnt++;  
                    Interlocked.Exchange(ref _locker, 0);
                    return true;
                }
                Interlocked.Exchange(ref _locker, 0);
            }
            return false;
        }
    }
}
