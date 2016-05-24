package goeasynet

import (
	"bufio"
	"bytes"
	"errors"
	"flag"
	"fmt"
	"io"
	"net"
	"runtime/debug"
	"sync"
	"time"
)

var host = flag.String("host", "localhost", "host")
var port = flag.String("port", "9000", "port")

var rpcid int
var proHeader = [2]byte{0x12, 0x34}

const proHeadLength = 8

const proErrClose = 1
const proRpcHandles = 2
const proTopicHandles = 3
const proRpcRequest = 4
const proRpcResponse = 5
const proPubData = 6
const proRpcErr = 7
const proPing = 8
const proPong = 9
const proOpen = 10
const proMAXLEN = 11

var (
	noconnecterr   = errors.New("No connect to data bus!")
	proceetimerout = errors.New("process timer out!")
)

type EasyNetGo struct {
	url      string
	subs     *map[string]func([]byte)
	rpchands *map[string]func([]byte) ([]byte, error)
	rpcs     *map[int]*rpcobj
	locker   *sync.Mutex
	wrbuf    chan *msgdata
	rrbuf    chan *msgdata
	conn     net.Conn
}
type msgdata struct {
	pro int16
	msg []byte
}
type rpcobj struct {
	k    int
	dly  int
	data []byte
	err  error
	easy *EasyNetGo
	cc   chan int
}

func newrpcobj(easy *EasyNetGo) *rpcobj {
	var r = rpcobj{}
	r.easy = easy
	r.cc = make(chan int, 10)
	return &r
}
func (this *rpcobj) call(data []byte, err error) {
	this.data = data
	this.err = err
	this.cc <- 1
}

func New(url string, pc int) *EasyNetGo {
	var r = &EasyNetGo{}
	r.subs = &map[string]func([]byte){}
	r.rpcs = &map[int]*rpcobj{}
	r.rpchands = &map[string]func([]byte) ([]byte, error){}
	r.locker = &sync.Mutex{}
	r.wrbuf = make(chan *msgdata, 10000)
	r.rrbuf = make(chan *msgdata, 10000)
	go r.taskRpcTimer()
	go r.taskSendPkt()
	go r.taskReadPkt(url)
	var i = 0
	for i = 0; i < pc; i++ {
		go r.taskProcessPkt()
	}
	return r
}
func (this *EasyNetGo) sendpkt(o *msgdata) {
	this.wrbuf <- o
}
func (this *EasyNetGo) saftRun(f func()) {
	this.locker.Lock()
	defer this.locker.Unlock()
	f()
}
func (this *EasyNetGo) RegSub(k string, f func([]byte)) {
	this.saftRun(func() {
		var _, ok = (*this.subs)[k]
		(*this.subs)[k] = f
		if !ok {
			this.syncSubs()
		}
	})
}
func (this *EasyNetGo) UnRegSub(k string) {
	this.saftRun(func() {
		var _, ok = (*this.subs)[k]
		if ok {
			delete(*this.subs, k)
			this.syncSubs()
		}
	})
}
func (this *EasyNetGo) syncSubs() {
	var data = msgdata{}
	data.pro = proTopicHandles
	var w = bytes.NewBuffer(nullbyte[0:0])
	pktwriteint(w, len(*this.subs))
	for k, _ := range *this.subs {
		pktwritestring(w, k)
	}
	data.msg = w.Bytes()
	this.sendpkt(&data)
}
func (this *EasyNetGo) RegHandle(k string, h func([]byte) ([]byte, error)) {
	this.saftRun(func() {
		var _, ok = (*this.rpchands)[k]
		(*this.rpchands)[k] = h
		if !ok {
			this.syncRpcHandles()
		}
	})
}
func (this *EasyNetGo) UnRegHandle(k string) {
	this.saftRun(func() {
		var _, ok = (*this.rpchands)[k]
		if !ok {
			delete(*this.rpchands, k)
			this.syncRpcHandles()
		}
	})
}
func (this *EasyNetGo) syncRpcHandles() {
	var data = msgdata{}
	data.pro = proRpcHandles
	var w = bytes.NewBuffer(nullbyte[0:0])
	pktwriteint(w, len(*this.rpchands))
	for k, _ := range *this.rpchands {
		pktwritestring(w, k)
	}
	data.msg = w.Bytes()
	this.sendpkt(&data)
}

var nullbyte [0]byte

func (this *EasyNetGo) Ping() {
	var data = msgdata{}
	data.pro = proPing
	data.msg = nullbyte[0:0]
	this.sendpkt(&data)
}
func pktwriteint(w *bytes.Buffer, v int) {
	w.WriteByte(byte(v >> 24))
	w.WriteByte(byte(v >> 16))
	w.WriteByte(byte(v >> 8))
	w.WriteByte(byte(v >> 0))
}
func pktwritestring(w *bytes.Buffer, buf string) {
	pktwritebytes(w, []byte(buf))
}
func pktwritebytes(w *bytes.Buffer, buf []byte) {
	if buf == nil {
		pktwriteint(w, -1)
	} else {
		pktwriteint(w, len(buf))
		w.Write(buf)
	}
}
func (this *EasyNetGo) Pub(k string, brocast bool, buf []byte) {
	var data = msgdata{}
	data.pro = proPubData

	var w = bytes.NewBuffer(nullbyte[0:0])
	if brocast {
		w.WriteByte(1)
	} else {
		w.WriteByte(0)
	}
	pktwritestring(w, k)
	pktwritebytes(w, buf)
	data.msg = w.Bytes()
	this.sendpkt(&data)
}

func (this *EasyNetGo) Rpc(k string, dly int, data []byte) ([]byte, error) {
	if this.conn == nil {
		return nil, noconnecterr
	}
	if dly <= 0 {
		dly = 1
	}
	var obj = newrpcobj(this)
	obj.dly = dly

	this.saftRun(func() {
		obj.k = rpcid
		(*this.rpcs)[rpcid] = obj
		rpcid++
	})
	//rpc encode
	var msg = msgdata{}
	msg.pro = proRpcRequest
	var w = bytes.NewBuffer(nullbyte[0:0])
	pktwriteint(w, obj.k)
	pktwriteint(w, 0)
	pktwriteint(w, 0)
	pktwritestring(w, k)
	pktwritebytes(w, data)
	msg.msg = w.Bytes()
	this.sendpkt(&msg)
	//wait rpc complted
	<-obj.cc
	return obj.data, obj.err
}
func catchErr() {
	if err := recover(); err != nil {
		debug.PrintStack()
	}
}
func (this *EasyNetGo) _taskRpcTimer(f bool) {
	defer catchErr()
	var nulls [0]*rpcobj
	var lst []*rpcobj
	lst = nulls[0:0]
	if f {
		this.saftRun(this.syncSubs)
		this.saftRun(this.syncRpcHandles)
	}
	this.saftRun(func() {
		for _, v := range *this.rpcs {
			if v.dly > 0 {
				v.dly = v.dly - 1
			} else {
				lst = append(lst, v)
			}
		}
		for _, v := range lst {
			delete(*this.rpcs, v.k)
		}
	})
	for _, v := range lst {
		v.call(nil, proceetimerout)
	}
}
func (this *EasyNetGo) taskRpcTimer() {
	var cnt = 0
	for {
		cnt++
		time.Sleep(time.Second)
		if cnt > 60*10 {
			cnt = 0
		}
		this._taskRpcTimer(cnt == 0)
	}
}
func (this *EasyNetGo) taskReadPkt(url string) {
	var dly = time.Second
	for {
		time.Sleep(dly)
		dly = time.Second * 10
		tcpAddr, err := net.ResolveTCPAddr("tcp", url)
		conn, err := net.DialTCP("tcp", nil, tcpAddr)
		if err != nil {
			fmt.Println("Error connecting:", err)
			continue
		} else {
			fmt.Println("connect ok.......", tcpAddr)
		}

		this.readconn(conn)
	}
}
func (this *EasyNetGo) _taskSendPkt(conn net.Conn, v *msgdata) {
	defer catchErr()
	var l = len(v.msg)
	if l < 0 {
		return
	}
	var h [8]byte
	h[0] = proHeader[0]
	h[1] = proHeader[1]
	h[2] = byte(v.pro >> 8)
	h[3] = byte(v.pro)
	h[4] = byte(l >> 24)
	h[5] = byte(l >> 16)
	h[6] = byte(l >> 8)
	h[7] = byte(l)
	conn.Write(h[0:8])
	conn.Write(v.msg)
}
func (this *EasyNetGo) taskSendPkt() {
	for v := range this.wrbuf {
		var cc = this.conn
		if cc == nil {
			continue
		}
		this._taskSendPkt(cc, v)
	}
}
func (this *EasyNetGo) _taskProcessPkt(v *msgdata) {
	defer catchErr()
	var data = v.msg
	var pro = v.pro
	if pro == proRpcResponse {
		//fmt.Println("proRpcResponse")
		this.parseRpcResponse(data)
	} else if pro == proRpcErr {
		//fmt.Println("proRpcErr")
		this.parseRpcErr(data)
	} else if pro == proRpcRequest {
		this.parseRpcRequest(data)
	} else if pro == proPubData {
		this.parsePubData(data)
	}
}
func (this *EasyNetGo) taskProcessPkt() {
	for v := range this.rrbuf {
		this._taskProcessPkt(v)
	}
}

func (this *EasyNetGo) parseRpcErr(buf []byte) {
	buf, id := rdInt(buf)
	//buf, src := rdInt(buf)
	//buf, dst := rdInt(buf)
	buf = buf[8:]
	buf, bd := rdArray(buf)
	var r *rpcobj
	var ok bool
	this.saftRun(func() {
		r, ok = (*this.rpcs)[id]
		if ok {
			delete(*this.rpcs, id)
		}
	})
	if ok {
		r.call(nil, errors.New(string(bd)))
	}
}
func (this *EasyNetGo) parseRpcResponse(buf []byte) {
	buf, id := rdInt(buf)
	//buf, src := rdInt(buf)
	//buf, dst := rdInt(buf)
	buf = buf[8:]
	buf, bd := rdArray(buf)
	var r *rpcobj
	var ok bool
	this.saftRun(func() {
		r, ok = (*this.rpcs)[id]
		if ok {
			delete(*this.rpcs, id)
		}
	})
	if ok {
		r.call(bd, nil)
	}
}
func (this *EasyNetGo) rpcRequestComplted(id int, src int, dst int, data []byte, err error) {
	var msg = msgdata{}
	msg.pro = proRpcRequest
	var w = bytes.NewBuffer(nullbyte[0:0])
	pktwriteint(w, id)
	pktwriteint(w, dst)
	pktwriteint(w, src)
	if err != nil {
		msg.pro = proRpcErr
		pktwritebytes(w, []byte(err.Error()))
	} else {
		msg.pro = proRpcResponse
		pktwritebytes(w, data)
	}
	msg.msg = w.Bytes()
	this.sendpkt(&msg)
}
func (this *EasyNetGo) excuteRpcHandle(buf []byte, handle func([]byte) ([]byte, error)) (data []byte, err error) {
	data = nil
	err = nil
	defer func() {
		if er := recover(); er != nil {
			if a, ok := er.(error); ok {
				err = a
			} else if a, ok := er.(string); ok {
				err = errors.New(a)
			} else {
				err = errors.New("unni")
			}
		}
		if err != nil {
			fmt.Println(err)
		}
	}()
	data, err = handle(buf)
	return
}
func (this *EasyNetGo) parseRpcRequest(buf []byte) {
	buf, id := rdInt(buf)
	buf, src := rdInt(buf)
	buf, dst := rdInt(buf)
	buf, kk := rdArray(buf)
	buf, bd := rdArray(buf)
	var r func([]byte) ([]byte, error)
	var ok bool
	this.saftRun(func() {
		r, ok = (*this.rpchands)[string(kk)]
	})
	var d []byte = nil
	var err error
	if ok {
		d, err = this.excuteRpcHandle(bd, r)
	} else {
		err = errors.New("no such rpc call")
	}
	if d == nil {
		d = nullbyte[0:0]
	}
	this.rpcRequestComplted(id, src, dst, d, err)
}
func (this *EasyNetGo) parsePubData(buf []byte) {
	//fmt.Println("subsssssssssssssssss")
	buf, _ = rdBool(buf)
	buf, kk := rdArray(buf)
	buf, bd := rdArray(buf)
	var r func([]byte)
	var ok bool
	this.saftRun(func() {
		r, ok = (*this.subs)[string(kk)]
	})
	if ok {
		r(bd)
	}
}
func (this *EasyNetGo) readconn(conn net.Conn) {
	defer func() {
		recover()
		this.saftRun(func() {
			if this.conn == conn {
				this.conn = nil
				conn.Close()
			}
			fmt.Println("connect close!")
		})
	}()
	this.saftRun(func() {
		this.conn = conn
		this.syncSubs()
		this.syncRpcHandles()
	})
	var reader = bufio.NewReader(conn)
	var _, hid = rdInt16(proHeader[:])
	for {
		head, e := readConnData(reader, proHeadLength)
		if e != nil {
			break
		}
		head, hhid := rdInt16(head)
		if hhid != hid {
			break
		}
		//fmt.Println("read data")
		head, pro := rdInt16(head)
		if pro >= proRpcHandles && pro <= proOpen {
			_, bdlen := rdInt(head)
			head, e = readConnData(reader, bdlen)
			if e != nil {
				break
			}
			var dt = msgdata{}
			dt.pro = pro
			dt.msg = head
			this.rrbuf <- &dt
		} else if pro == proMAXLEN {
			continue
		} else {
			break
		}
	}
}
func readConnData(r *bufio.Reader, cnt int) ([]byte, error) {
	var a [0]byte
	var dst = bytes.NewBuffer(a[0:])
	_, e := io.CopyN(dst, r, int64(cnt))
	return dst.Bytes(), e
}
func rdBool(buf []byte) ([]byte, bool) {
	var b = buf[0] != 0
	return buf[1:], b
}
func sliceNext(buf []byte, cnt int) []byte {
	if len(buf) > cnt {
		return buf[cnt:]
	}
	return nil
}
func sliceSub(buf []byte, offset int, cnt int) []byte {
	if len(buf) >= (cnt + offset) {
		return buf[offset : offset+cnt]
	}
	return nil
}
func rdInt(buf []byte) ([]byte, int) {
	i := 0
	a := 0
	for i = 0; i < 4; i++ {
		a <<= 8
		a += int(buf[i])
	}
	return sliceNext(buf, 4), a
}
func rdInt16(buf []byte) ([]byte, int16) {
	i := 0
	a := 0
	for i = 0; i < 2; i++ {
		a <<= 8
		a += int(buf[i])
	}
	return sliceNext(buf, 2), int16(a)
}
func rdArray(buf []byte) (n []byte, v []byte) {
	n = buf[0:0]
	v = n
	var s1, ll = rdInt(buf)
	if ll < 0 {
		ll = 0
	} else {
		v = s1[0:ll]
	}
	var sy = len(s1) - ll
	if sy > 0 {
		n = s1[ll:]
	}
	return
}
