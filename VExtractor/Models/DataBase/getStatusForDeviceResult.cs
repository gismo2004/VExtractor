﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VExtractor.Models.DataBase
{
    public partial class getStatusForDeviceResult
    {
        public long Id { get; set; }
        public byte CompanyId { get; set; }
        public int EventTypeId { get; set; }
        public int? EventTypeName { get; set; }
        public int EventValueTypeId { get; set; }
        public double? EventValue_Float { get; set; }
        public int? EventValue_Int { get; set; }
        public string EventValue_VarChar { get; set; }
        public string EventValue_NText { get; set; }
        public bool? EventValue_Bit { get; set; }
        public byte[] EventValue_Binary { get; set; }
        public DateTime? EventValue_DateTime { get; set; }
        public string Unit { get; set; }
        public string DataType { get; set; }
        public int DeviceId { get; set; }
        public int? DeviceName { get; set; }
        public int DataPointId { get; set; }
        public int? DataPointName { get; set; }
        public DateTime EventTime { get; set; }
        public int? EventPriority { get; set; }
        public int? StatusTypeId { get; set; }
        public DateTime EventReceivedTime { get; set; }
    }
}