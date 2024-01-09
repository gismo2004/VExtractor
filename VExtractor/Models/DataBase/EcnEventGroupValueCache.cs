﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnEventGroupValueCache
{
    public byte CompanyId { get; set; }

    public int DeviceId { get; set; }

    public int DataPointId { get; set; }

    public int EventTypeId { get; set; }

    public int EventValueTypeId { get; set; }

    public byte ValueState { get; set; }

    public int? StatusTypeId { get; set; }

    public int? ValueType { get; set; }

    public bool? ValueBit { get; set; }

    public int? ValueInt { get; set; }

    public double? ValueFloat { get; set; }

    public string ValueNtext { get; set; }

    public string ValueVarChar { get; set; }

    public DateTime? ValueDateTime { get; set; }

    public byte[] ValueBinary { get; set; }

    public string ValueUnit { get; set; }

    public DateTime ValueEventTime { get; set; }

    public virtual EcnCompany Company { get; set; }

    public virtual EcnDataPoint EcnDataPoint { get; set; }

    public virtual EcnDevice EcnDevice { get; set; }

    public virtual EcnEventValueType EcnEventValueType { get; set; }

    public virtual EcnStatusType EcnStatusType { get; set; }
}