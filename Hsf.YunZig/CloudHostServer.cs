using Codeplex.Data;
using Hsf.DAL;
using Hsf.Model;
using Hsf.Redis.Service;
using Hsf.YunZig;
using Newtonsoft.Json;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Hsf.YunZig
{
    /// <summary>
    /// 服务端代码辅助类
    /// </summary>
    public class CloudHostServer
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger("CloudHostServer");
        static int connectCount = 0;
        //static int receiveCount = 0;
        static Encoding encoding = Encoding.GetEncoding("gbk");
        HansAppServer myServer = new HansAppServer(encoding, "{\"", "}");//{\"code\":", "}
        public static ConcurrentDictionary<string, HansAppSession> Gateway_SessionDic = new ConcurrentDictionary<string, HansAppSession>();
        public static HansAppSession SmartHome_Session = new HansAppSession();


        public void Listen()
        {
            var config = new SuperSocket.SocketBase.Config.ServerConfig()
            {
                //Name = "SSServer",
                //ServerTypeName = "SServer",
                ClearIdleSession = true, //120秒执行一次清理90秒没数据传送的连接
                ClearIdleSessionInterval = 120,
                IdleSessionTimeOut = 90,
                MaxRequestLength = 8192, //最大包长度
                Ip = "Any",
                Port = 9004,
                MaxConnectionNumber = 100000,
            };

            //Setup the appServer
            if (!myServer.Setup(config)) //Setup with listening port
            {
                log.Info("云主机 Failed to setup!");
                return;
            }
            //Try to start the appServer
            if (!myServer.Start())
            {
                log.Info("云主机 Failed to start!");
                return;
            }
            myServer.NewSessionConnected += MyServer_NewSessionConnected;
            myServer.NewRequestReceived += MyServer_NewRequestReceived;
            myServer.SessionClosed += myServer_SessionClosed;
            log.Info($"云主机开启。。");
            Console.WriteLine($"云主机开启 {DateTime.Now.ToString()}");
        }

        private void MyServer_NewRequestReceived(HansAppSession session, HansRequestInfo requestInfo)
        {
            Task.Run(() =>
            {
                string msg = requestInfo.Body.StartMark + requestInfo.Body.BodyString + requestInfo.Body.EndMark;//encoding.GetString(requestInfo.Body.BodyBuffer);
                string[] sArray = Regex.Split(msg, "}{", RegexOptions.IgnoreCase);
                for (int i = 0; i < sArray.Length; i++)
                {
                    if (!sArray[i].StartsWith("{"))
                    {
                        sArray[i] = "{" + sArray[i];
                    }
                    int _left = Regex.Matches(sArray[i], "{").Count;
                    int _right = Regex.Matches(sArray[i], "}").Count;
                    int cha = _left - _right;
                    for (int b = 0; b < cha; b++)
                    {
                        sArray[i] = sArray[i] + "}";
                    }

                    var res = DynamicJson.Parse(sArray[i]);
                    if (res.IsDefined("code"))
                    {
                        double code = res.code;
                        if (code == 101)
                        {
                            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            session.Send("{\"code\":1001,\"res\":0,\"timestamp\": " + Convert.ToInt64(ts.TotalSeconds) + "}");
                            break;
                        }
                        if (code == 010)
                        {
                            session.Send("alive");
                            break;
                        }
                        else
                        {
                            log.Debug($"收到消息： {msg}");
                            switch (code)
                            {
                                //102为什么不可以？手动操作的时候只有104，没有102
                                case 102://{"code":102,"id":"010000124b000f81eea6","ep":8,"serial":1,"control":{"on":true,"pt":100},"result":0}
                                    if (res.IsDefined("result"))
                                    {
                                        string state = ""; //离线为""
                                        using (RedisHashService service = new RedisHashService())
                                        {
                                            if (res.result == 0)//成功0，失败3离线
                                            {
                                                if (res.IsDefined("control"))//可能有不存在on的
                                                {
                                                    var control = res.control;
                                                    if (control.IsDefined("on"))
                                                    {
                                                        state = control.on.ToString();
                                                    }
                                                    else if (control.IsDefined("pt"))
                                                    {
                                                        state = Convert.ToInt32(control.pt) > 0 ? "True" : "False";//窗帘开度,存在关闭的时候104关闭不充分，打开13，关闭86
                                                    }
                                                    else
                                                    {
                                                        state = "False";//在线没有st，但是没有开关状态的情况，默认关闭
                                                    }
                                                }
                                                else
                                                {
                                                    state = "False";//在线没有st，但是没有开关状态的情况，默认关闭
                                                }
                                            }
                                            string cachekey = res.id + "_" + res.ep;
                                            //发布YunZigStateChangeQueue状态同步消息
                                            PutMqttData(service, cachekey, state);
                                        }
                                    }
                                    break;
                                case 104://手动，app无法区分{"code":104,"control":2,"id":"010000124b0014c6aaee","ol":true,"ep":1,"pid":260,"did":0,"st":{"on":false}}
                                    if (res.IsDefined("control"))
                                    {
                                        if (res.control == 2)
                                        {
                                            string state = ""; //离线为""
                                            using (RedisHashService service = new RedisHashService())
                                            {
                                                if (res.ol == true)//在线
                                                {
                                                    if (res.IsDefined("st"))//可能有不存在on的
                                                    {
                                                        var st = res.st;
                                                        if (st.IsDefined("on"))
                                                        {
                                                            state = st.on.ToString();
                                                        }
                                                        else if (st.IsDefined("pt"))
                                                        {
                                                            state = Convert.ToInt32(st.pt) > 0 ? "True" : "False";//窗帘开度,存在关闭的时候104关闭不充分，打开13，关闭86
                                                        }
                                                        else
                                                        {
                                                            state = "False";//在线没有on，但是没有开关状态的情况，默认关闭
                                                        }
                                                    }
                                                    else
                                                    {
                                                        state = "False";//在线没有st，但是没有开关状态的情况，默认关闭
                                                    }
                                                }

                                                string cachekey = res.id + "_" + res.ep;
                                                //发布YunZigStateChangeQueue状态同步消息
                                                PutMqttData(service, cachekey, state);
                                            }
                                        }
                                    }
                                    break;
                                case 1002://改变设备状态
                                case 5001://查询某个设备状态
                                    if (res.IsDefined("zigbee"))
                                    {
                                        if (Gateway_SessionDic.ContainsKey(res.zigbee))
                                        {
                                            Gateway_SessionDic[res.zigbee].Send(sArray[i]);
                                        }
                                        else
                                        {
                                            log.Debug($"请求网关的session不存在 {res.zigbee}： {msg}");
                                        }
                                    }
                                    break;
                                case 501:
                                    using (RedisHashService service = new RedisHashService())
                                    {
                                        foreach (var item in res.device)//多键开关
                                        {
                                            string state = ""; //离线为""
                                            if (item.ol == true)//在线
                                            {
                                                if (item.IsDefined("st"))//可能有不存在on的
                                                {
                                                    var st = item.st;
                                                    if (st.IsDefined("on"))
                                                    {
                                                        state = st.on.ToString();
                                                    }
                                                    else if (st.IsDefined("pt"))
                                                    {
                                                        state = Convert.ToInt32(st.pt) > 0 ? "True" : "False";//窗帘开度,存在关闭的时候104关闭不充分，打开13，关闭86
                                                    }
                                                    else
                                                    {
                                                        state = "False";//在线没有on，但是没有开关状态的情况，默认关闭
                                                    }
                                                }
                                                else
                                                {
                                                    state = "False";//在线没有on，但是没有开关状态的情况，默认关闭
                                                }
                                            }
                                            service.SetEntryInHash("DeviceStatus", res.id + "_" + res.ep, state);
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    else if (res.IsDefined("mac") && res.IsDefined("id"))
                    {
                        //缓存当前socket连接，初次连接心跳之前会收到网关注册信息{"id":"00ff2c2c2c6a6f005979","mac":"2c:6a:6f:00:59:79"}
                        //Func<string, HansAppSession, HansAppSession> dd = (key,oldValue)=>session;
                        //当网关重新连接的时候，ip换了的时候，替换之前的session
                        Gateway_SessionDic.AddOrUpdate(res.id, session, (Func<string, HansAppSession, HansAppSession>)((key, oldValue) => session));

                        using (RedisHashService service = new RedisHashService())
                        {
                            service.SetEntryInHash("DeviceStatus", res.id + "_13579", "True");//mac_port,
                            //网关会自动上传104所有设备状态
                        }
                        log.Debug($"收到网关注册消息：缓存session： {session.RemoteEndPoint} ，网关状态信息： {msg}");
                        Console.WriteLine($"连接网关Ip： {session.RemoteEndPoint} Mac： {res.mac} {DateTime.Now.ToString()}");
                    }
                    else if (res.IsDefined("lot"))
                    {
                        SmartHome_Session = session;
                        log.Debug($"收到Lot长连接session： {session.RemoteEndPoint} 信息： {msg}");
                    }
                    else
                    {
                        log.Info($"{session.RemoteEndPoint.ToString()} 未识别信息： {sArray[i]}");
                    }
                }
            });

        }

        private void MyServer_NewSessionConnected(HansAppSession session)
        {
            //session.Send("Welcome to SuperSocket Telnet Server\r\n");
            connectCount++;
            Console.WriteLine($"{session.RemoteEndPoint.ToString()} 登录到服务器 {connectCount} {DateTime.Now.ToString()}");
            log.Info($"{session.RemoteEndPoint.ToString()} 登录到服务器 {connectCount}");
        }

        void myServer_SessionClosed(HansAppSession session, SuperSocket.SocketBase.CloseReason value)
        {
            foreach (var item in Gateway_SessionDic)
            {
                if (item.Value == session)
                {
                    HansAppSession dd = null;
                    Gateway_SessionDic.TryRemove(item.Key, out dd);
                    using (RedisHashService service = new RedisHashService())
                    {
                        service.SetEntryInHash("DeviceStatus", item.Key + "_13579", "");//设置离线,
                        log.Info($"{item.Key} 离开服务器");
                        //网关下的所有设备状态设置为离线
                        using (HsfDBContext db = new HsfDBContext())
                        {
                            var channelList = db.host_device.Where(t => t.devchannel == item.Key && t.deletemark == 0);
                            foreach (var item2 in channelList)
                            {
                                service.SetEntryInHash("DeviceStatus", item2.cachekey, "");//设置离线
                            }
                        }
                    }
                }
            }
            connectCount--;
            Console.WriteLine($"{session.RemoteEndPoint.ToString()} 离开服务器 {connectCount} {DateTime.Now.ToString()}");
            log.Info($"{session.RemoteEndPoint.ToString()} 离开服务器 {connectCount}");

            //throw new NotImplementedException();
        }


        public static void PutMqttData(RedisHashService service,string cachekey,string state)
        {
            //如果状态已经是当前要操作的状态
            string state0 = service.GetValueFromHash("DeviceStatus", cachekey);
            //不一致的状态再去修改
            if (state0 != state)
            {
                service.SetEntryInHash("DeviceStatus", cachekey, state);
                string openUid = service.GetValueFromHash("DuerOSOpenUid_Device", cachekey);
                //存在百度音响openUid的同步到百度音响平台
                if (!string.IsNullOrEmpty(openUid))
                {
                    using (RedisListService service2 = new RedisListService())
                    {
                        service2.Publish("YunZigStateChangeQueue", cachekey);
                        log.Debug($"《《《《《《《《《《《《《《《《发布YunZigStateChangeQueue状态同步消息 {cachekey}");
                    }
                }
            }
        }

        public static void GetMqttData()
        {
            Task.Run(() =>
            {
                try
                {
                    using (RedisListService service = new RedisListService())
                    {
                        while (true)
                        {
                            log.Info($"》》》》》》》》》》》》循环队列获取的消息");
                            string result = service.BlockingPopItemFromList("DuerOSControlQueue", TimeSpan.FromMinutes(10));
                            if (!string.IsNullOrEmpty(result))
                            {
                                //Console.WriteLine($"***********队列获取的消息 {result} {DateTime.Now.ToString()}");
                                log.Info($" 》》》》》》》》》》》》队列获取的消息 {result}");
                                if (result.Contains("$"))
                                {
                                    string cachekey = result.Split('$')[0];
                                    string state = result.Split('$')[1];
                                    ChangeStateMain.DeviceStateChange(cachekey, Convert.ToBoolean(state));
                                }
                                Thread.Sleep(100);
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    log.Info($"获取改变状态队列异常： "+ex.Message);
                    throw;
                }

            });



            //Action act = new Action(() =>
            //{
            //    while (true)
            //    {
            //        log.Info($"***********循环队列获取的消息");
            //        string result = service.BlockingPopItemFromList("BaiDuSoundControl", TimeSpan.FromHours(3));
            //        //Console.WriteLine($"***********队列获取的消息 {result} {DateTime.Now.ToString()}");
            //        log.Info($" ***********队列获取的消息 {result}");
            //        if (result.Contains("$"))
            //        {
            //            string cachekey = result.Split('$')[0];
            //            string state = result.Split('$')[1];
            //            ChangeStateMain.DeviceStateChange(cachekey, Convert.ToBoolean(state));
            //        }
            //        Thread.Sleep(100);
            //    }
            //});
            //act.EndInvoke(act.BeginInvoke(null, null));
        }
    }
}
