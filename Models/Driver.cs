using System;
using System.Collections.Generic;
using System.Text;

namespace APP_GoiXetheoGPS.Models
{
    public class Driver
    {
        public int DriverID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int RegionID { get; set; }
        public bool IsActive { get; set; } = true;   // true = hoạt động, false = bị khóa
    }
}
