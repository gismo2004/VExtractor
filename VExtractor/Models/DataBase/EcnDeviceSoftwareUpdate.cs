﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnDeviceSoftwareUpdate
{
    public byte CompanyId { get; set; }

    public int DeviceId { get; set; }

    public string UpdateName { get; set; }

    public int? UpdateStatus { get; set; }

    public string MajorSoftwareVersion { get; set; }

    public string MinorSoftwareVersion { get; set; }

    public string UpdateUsingIdentification { get; set; }

    public DateTime? UpdateTime { get; set; }

    public DateTime? UpdateReadTime { get; set; }
}