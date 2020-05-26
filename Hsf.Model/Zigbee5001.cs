using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hsf.Model
{
    public class Zigbee5001
    {
        /// <summary>
        /// 
        /// </summary>
        public int code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int serial { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<DeviceItem> device { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string zigbee { get; set; }
    }

    //public class DeviceItem
    //{
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    public string id { get; set; }
    //}
}
