using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EasyNet.Imp;

namespace c1test {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
            _node.OnPongMsg = () => { Console.WriteLine("Pong->>" + NowStr()); };
        }
        string NowStr() {
            return DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");                        //    127,0,0,1 192, 168, 168, 229
        }

        EasyClient _node = new EasyClient(new IPEndPoint(new IPAddress(new byte[] { 192, 168, 168, 229 }), 9000), DateTime.Now.Ticks.ToString());
        private void button1_Click(object sender, EventArgs e) {
            _node.Start(() => { Console.WriteLine(_node.Active.ToString()); });
            _node.RegSub("wuxi", buf => {
                Console.WriteLine("Sub" + Encoding.UTF8.GetString(buf));
            });
        }
        private void button2_Click(object sender, EventArgs e) {
            string s = textBox1.Text.Trim();
            _node.RegHandle(s, (r, c) => {
                Console.WriteLine("->>req:" + Encoding.UTF8.GetString(r));
                var sx = s + ".." + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
                var buf = Encoding.UTF8.GetBytes(sx);
                //c(null, new Exception("client process exception!"));
                c(buf, null);
            });
        }

        private void button3_Click(object sender, EventArgs e) {
            int cnt = 10;
            int rr = cnt;
            DateTime ks = DateTime.Now;
            DateTime sj = DateTime.Now;
            int a = 0;
            for (int i = 0; i < cnt; i++) {
                string s = textBox2.Text.Trim();
                var m = ">>" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
                //Console.WriteLine(m);
                var buf = Encoding.UTF8.GetBytes(m);
                _node.Rpc(s, 30 * 1000, buf, (b, ee) => {
                    var r = Interlocked.Decrement(ref rr);
                    if (r <= 0) {
                        var span = DateTime.Now - ks;
                        Console.WriteLine("___________________");
                        Console.WriteLine(span.TotalSeconds.ToString());
                    }
                    //return;
                    int mm = Interlocked.Increment(ref a);
                    if (mm > cnt) {
                        Console.WriteLine((DateTime.Now - sj).TotalSeconds.ToString() + "______________");
                    }
                    if (ee != null) {
                        Console.WriteLine(ee.Message);
                    }
                    Console.WriteLine("<<" + Encoding.UTF8.GetString(b));
                    //var buf = Encoding.UTF8.GetBytes(s + ".." + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
                });
            }
        }

        private void button4_Click(object sender, EventArgs e) {
            string s = textBox3.Text.Trim();
            var m = ">>" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
            Console.WriteLine("Pub" + m);
            var buf = Encoding.UTF8.GetBytes(m);
            _node.Pub("wuxi", true, buf);
        }

        private void button5_Click(object sender, EventArgs e) {
            _node.Ping();
            Console.WriteLine("Ping->>" + NowStr());
        }
    }
}
