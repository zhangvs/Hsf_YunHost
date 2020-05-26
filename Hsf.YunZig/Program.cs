using Hsf.Framework;
using Hsf.Redis.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hsf.YunZig
{
    class Program
    {
        //使用引用类库的功能的情况下，必须添加调用类库的包，比如mysql，newjson
        //private static RedisHashService service = new RedisHashService();
        static void Main(string[] args)
        {
            //using (RedisHashService service1 = new RedisHashService())
            //{
            //    var dd= service1.SearchKeys("*Device*");
            //    //service1.SetEntryInHash("Baidu_OpenUid", "010000124b00172f668f_1", "ee65663a03e810214acc0ac1c18260e7");
            //    //service1.RemoveEntryFromHash("OutDevices", "MMSJ*");
            //}

            //using (RedisListService service2 = new RedisListService())
            //{
            //    service2.Publish("LotDeviceChangeQueue", "AAA");
            //}

            //SmsCore.SendWarn("18660996839", "红外");
            //service.HashGet("Room", "123");
            //启动云主机
            CloudHostServer cloudHostServer = new CloudHostServer();
            cloudHostServer.Listen();
            //YuiYingServer yuiYingServer = new YuiYingServer();
            //yuiYingServer.Listen();

            CloudHostServer.GetMqttData();
            Console.ReadKey();
        }
    }
}
