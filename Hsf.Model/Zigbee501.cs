using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hsf.Model
{
    public class Zigbee501
    {
        /// <summary>
        /// 
        /// </summary>
        public long timestamp { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<DeviceItem> device { get; set; }
    }

}
