using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNet.Base {
    /// <summary>
    /// 最小堆.
    /// </summary>
    public class MinHeap<T> {
        protected List<T> _baseHeap;
        private IComparer<T> _comparer;

        public MinHeap()
            : this(Comparer<T>.Default) {
        }

        public MinHeap(IComparer<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException();

            _baseHeap = new List<T>();
            _comparer = comparer;
        }
        public void Enqueue(T v) {
            Insert(v);
        }

        public void Clear() {
            _baseHeap.Clear();
        }

        private void Insert(T v) {
            _baseHeap.Add(v);

            // heapify after insert, from end to beginning
            HeapifyFromEndToBeginning(_baseHeap.Count - 1);
        }

        private int HeapifyFromEndToBeginning(int pos) {
            if ((pos >= _baseHeap.Count) || (pos < 0)) return -1;

            // heap[i] have children heap[2*i + 1] and heap[2*i + 2] and parent heap[(i-1)/ 2];
            var v = _baseHeap[pos];
            while (pos > 0) {
                int parentPos = (pos - 1) / 2;
                if (_comparer.Compare(_baseHeap[parentPos], v) > 0) {
                    //ExchangeElements(parentPos, pos);
                    _baseHeap[pos] = _baseHeap[parentPos];
                    pos = parentPos;
                }
                else break;
            }
            _baseHeap[pos] = v;
            return pos;
        }

        public T Dequeue() {
            if (!IsEmpty) {
                T result = _baseHeap[0];
                DeleteRoot();
                return result;
            }
            else
                throw new InvalidOperationException("min heap is empty");
        }

        private void DeleteRoot() {
            if (_baseHeap.Count <= 1) {
                _baseHeap.Clear();
                return;
            }

            _baseHeap[0] = _baseHeap[_baseHeap.Count - 1];
            _baseHeap.RemoveAt(_baseHeap.Count - 1);

            // heapify
            HeapifyFromBeginningToEnd(0);
        }

        private int HeapifyFromBeginningToEnd(int pos) {
            if ((pos >= _baseHeap.Count) || (pos < 0)) return -1;
            // heap[i] have children heap[2*i + 1] and heap[2*i + 2] and parent heap[(i-1)/ 2];
            var v = _baseHeap[pos];
            while (true) {
                // on each iteration exchange element with its smallest child
                int left = 2 * pos + 1;
                int right = 2 * pos + 2;
                int smallest = left;
                if (left >= _baseHeap.Count) {
                    break;
                }
                if (right < _baseHeap.Count && _comparer.Compare(_baseHeap[left], _baseHeap[right]) > 0) {
                    smallest = right;
                }
                if (_comparer.Compare(v, _baseHeap[smallest]) <= 0) {
                    break;
                }
                _baseHeap[pos] = _baseHeap[smallest];
                pos = smallest;
            }
            _baseHeap[pos] = v;
            return pos;
        }
        public T Peek() {
            if (!IsEmpty) {
                return _baseHeap[0];
            }
            throw new InvalidOperationException("min heap is empty");
        }

        public int Count {
            get { return _baseHeap.Count; }
        }

        public bool IsEmpty {
            get { return _baseHeap.Count == 0; }
        }
        public bool Remove(T item) {
            // find element in the collection and remove it
            int elementIdx = _baseHeap.IndexOf(item);
            return Remove(elementIdx);
        }
        public bool Remove(int elementIdx) {
            if (elementIdx < 0) return false;
            if (elementIdx >= _baseHeap.Count) return false;

            //remove element
            _baseHeap[elementIdx] = _baseHeap[_baseHeap.Count - 1];
            _baseHeap.RemoveAt(_baseHeap.Count - 1);

            // heapify
            int newPos = HeapifyFromEndToBeginning(elementIdx);
            if (newPos == elementIdx)
                HeapifyFromBeginningToEnd(elementIdx);

            return true;
        }
        public T this[int index] {
            get { return _baseHeap[index]; }
        }
    }
}
