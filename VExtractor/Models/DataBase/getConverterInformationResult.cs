﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VExtractor.Models.DataBase
{
    public partial class getConverterInformationResult
    {
        public int? DataPointId { get; set; }
        public string DataPointName { get; set; }
        public int? EventTypeId { get; set; }
        public string EventTypeName { get; set; }
        public bool? EventEnumType { get; set; }
        public int? EventValueTypeId { get; set; }
        public string DeviceName { get; set; }
        public int? EventPriority { get; set; }
        public int? StatusTypeId { get; set; }
        public long? LastEventValueTypeId { get; set; }
        public int? DataPointTypeId { get; set; }
    }
}
