﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmOperationsDiary
{
    public Guid Guid { get; set; }

    public byte CompanyId { get; set; }

    public int DeviceId { get; set; }

    public int? DatapointId { get; set; }

    public int UserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int RelevantFor { get; set; }

    public int Type { get; set; }

    public string Text { get; set; }

    public virtual EcnDataPoint EcnDataPoint { get; set; }

    public virtual EcnDevice EcnDevice { get; set; }

    public virtual EcnUser EcnUser { get; set; }
}