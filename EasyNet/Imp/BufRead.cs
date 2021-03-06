﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNet.Imp {
    class BufRead {
        byte[] _buf;
        int _offset;
        int _length;
        int _cnt;

        public void Init(byte[] buf, int offset, int length) {
            _buf = buf;
            _offset = offset;
            _length = length;
            if (offset < buf.Length) {
                _cnt = buf[offset];
            }
        }
        public int ReadInt() {
            if (_length < 4) {
                throw new Exception("no such data!");
            }
            int i = _buf[_offset++];
            i <<= 8;
            i += _buf[_offset++];
            i <<= 8;
            i += _buf[_offset++];
            i <<= 8;
            i += _buf[_offset++];
            _length -= 4;
            return i;
        }
        public byte[] ReadBytes(out int offset, out int length) {
            length = ReadInt();
            offset = _offset;
            if (length < 0) {
                return null;
            }
            _offset += length;
            _length -= length;
            return _buf;
        }
        public byte[] ReadBytes() {
            int offset, length;
            ReadBytes(out offset, out length);
            if (length < 0) {
                return null;
            }
            var buf = new byte[length];
            Array.Copy(_buf, offset, buf, 0, length);
            return buf;
        }
        public string ReadString() {
            int offset;
            int length;
            ReadBytes(out offset, out length);
            if (length < 0) {
                return null;
            }
            return Encoding.UTF8.GetString(_buf, offset, length);
        }

        public HashSet<string> ReadHashSet() {   
            int len = ReadInt();
            if (len < 0) {
                return null;
            }
            var hs = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < len; i++) {
                hs.Add(ReadString());
            }
            return hs;
        }

        public byte ReadByte() {
            byte b = _buf[_offset++];
            _length--;
            return b;
        }
    }
}
