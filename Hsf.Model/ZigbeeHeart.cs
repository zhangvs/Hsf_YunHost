using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hsf.Model
{
    public class Gw
    {
        /// <summary>
        /// 
        /// </summary>
        public string ver { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string mac { get; set; }
    }

    public class ZigbeeHeart
    {
        /// <summary>
        /// 
        /// </summary>
        public int code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long timestamp { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Gw gw { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<string> device { get; set; }
    }
}
