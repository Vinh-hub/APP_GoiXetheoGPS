using System;
using System.Collections.Generic;
using System.Text;

namespace APP_GoiXetheoGPS.Models
{
    public class Trip
    {
        public int UserID { get; set; }
        public int DriverID { get; set; }
        public string Status { get; set; }
        public decimal Price { get; set; }
        public double Latitude { get; set; }
    }
}
