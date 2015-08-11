using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using EasyNet.Imp;

namespace c11 {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
            //
        }
        EasyGateway api = new EasyGateway();
        private void Form1_Load(object sender, EventArgs e) {
            api.SetEP(new IPEndPoint(IPAddress.Any, 9000));
            api.Start();
        }

        private void button1_Click(object sender, EventArgs e) {
            GC.Collect();
        }
    }
}
