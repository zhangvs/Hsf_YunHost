using Hsf.DAL;
using Hsf.Model;
using Hsf.Redis.Service;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hsf.YunZig
{
    public class ChangeStateMain
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger("ChangeStateMain");

        #region 设备状态改变8145/8135
        /// <summary>
        /// 8145关闭设备 8;8135;设备id   
        /// user:123_DCD9165057AD type:other msg:123_DCD9165057AD;8;8145;01120925117040;3,0$/r$
        /// 8135打开设备 8;8135;设备id
        /// user:123_DCD9165057AD type:other msg:123_DCD9165057AD;8;8135;01120925117040;2;8$/r$
        /// user:123_Server type:other msg:123_e0ddc0a405d9;8;8135;A$/r$
        /// {"code":1002,"id":"010000124b0014c6aaee","ep":1,"serial":1,"control":{"on":true},"result":0,"zigbee":"00ff2c2c2c6a6f005979"}
        /// user:DAJCHSF_% type:other msg:DAJCHSF_Server;devrefresh;1041656180510,true,DAJCHSF_2047DABEF936$/r$
        /// 
        /// user:MMSJ-1#1-5-501 type:other msg:MMSJ-1-1-5-501;8;8145;08$/r$
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="cachekey"></param>
        public static bool DeviceStateChange(string cachekey, bool state)//, string code, bool state, string success, out string relayUser
        {
            try
            {
                using (RedisHashService service = new RedisHashService())
                {
                    string deviceEntityStr = service.GetValueFromHash("DeviceMacEntity", cachekey);//8231有关联，改mac的情况下，其它改状态，改名称，不需要清理
                    host_device deviceEntity = null;
                    if (!string.IsNullOrEmpty(deviceEntityStr))
                    {
                        deviceEntity = JsonConvert.DeserializeObject<host_device>(deviceEntityStr);//设备实体缓存
                    }
                    else
                    {
                        using (HsfDBContext hsfDBContext = new HsfDBContext())
                        {
                            deviceEntity = hsfDBContext.host_device.Where(t => t.cachekey == cachekey && t.deletemark == 0).FirstOrDefault();//注意device的唯一性
                            if (deviceEntity != null)
                            {
                                //缓存设备id与设备实体对应关系，避免查询数据库
                                service.SetEntryInHash("DeviceMacEntity", cachekey, JsonConvert.SerializeObject(deviceEntity));
                            }
                        }
                    }

                    if (deviceEntity != null)
                    {
                        //拼装1002指令，发送给网关，执行改变状态操作
                        return StateChangeByType(deviceEntity, state);
                    }
                    else
                    {
                        //relayUser = appUser;
                        return false;//error
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        /// <summary>
        /// 状态改变具体执行方法
        /// </summary>
        /// <param name="deviceEntity">设备实体</param>
        /// <param name="state">要改变成什么状态</param>
        /// <returns></returns>
        public static bool StateChangeByType(host_device deviceEntity, bool state)
        {
            object obj = null;
            switch (deviceEntity.devtype)
            {
                case "Smart_ZigbeeCurtain"://zigbee窗帘
                    obj = new { pt = state ? 100 : 0 };
                    return YunZigSendMsg(deviceEntity, obj, state);
                case "Panel_Zigbee"://zigbee开关
                    obj = new { on = state };
                    return YunZigSendMsg(deviceEntity, obj, state);
                default:
                    return false;
            }
        }

        #region zigbee网关
        /// <summary>
        /// zigbee网关发送
        /// 拼装1002指令，发送给网关，执行改变状态操作
        /// 1.Zigbee窗帘(Smart_ZigbeeCurtain)：pt控制窗帘开度百分比，0为全关闭，100为全打开
        /// 速度：打开窗帘230、4289。关闭窗帘229、3599  等104速度太慢，存在关闭的时候104关闭不充分，打开13，关闭86
        /// {"code":1002,"id":"010000124b000f81eea6","ep":8,"serial":1,"control":{"on":false,"pt":0},"result":0,"zigbee":"00ff2c2c2c6a6f0057f3"}//关闭
        /// 2.Zigbee开关(Panel_Zigbee)：on  
        /// 速度：打开开关239、392。关闭开关228、411
        /// {"code":1002,"id":"010000124b0014c5d116","ep":1,"serial":1,"control":{"on":true},"result":0,"zigbee":"00ff2c2c2c6a6f0057f3"}
        /// </summary>
        /// <param name="deviceEntity"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public static bool YunZigSendMsg(host_device deviceEntity, object obj, bool state)
        {
            string cachekey = deviceEntity.cachekey;//缓存处理--
            //发送到zigbee网关并遍历状态
            if (obj != null)
            {
                Zigbee1002 zigbee1002 = new Zigbee1002()
                {
                    code = 1002,
                    id = deviceEntity.devmac,//mac地址，010000124b0014c6aaee
                    ep = Convert.ToInt32(deviceEntity.devport),//端口
                    serial = 1,
                    control = obj,
                    result = 0
                    //zigbee = deviceEntity.devchannel//网关，00ff2c2c2c6a6f005979
                };
                string _1002 = JsonConvert.SerializeObject(zigbee1002);
                

                if (CloudHostServer.Gateway_SessionDic.ContainsKey(deviceEntity.devchannel))
                {
                    CloudHostServer.Gateway_SessionDic[deviceEntity.devchannel].Send(_1002);
                    return true;
                }
                else
                {
                    log.Debug($"请求网关的session不存在 {deviceEntity.devchannel}： {_1002}");
                    return false;
                }
            }
            else
            {
                log.Debug($"设备状态改变失败！cachekey：{cachekey}！ 不存在设备类型：{deviceEntity.devtype}");
                return false;
            }
        }
        #endregion
    }
}