﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnVwDataPointCurrentEventCurrentControl
{
    public byte CompanyId { get; set; }

    public int DeviceId { get; set; }

    public int DataPointTypeId { get; set; }

    public int DataPointId { get; set; }

    public string Unit { get; set; }

    public string EventTypeAdress { get; set; }

    public int EventTypeId { get; set; }

    public string EventTypeConversion { get; set; }

    public string EventTypeDefaultValue { get; set; }

    public DateTime? EventLogTime { get; set; }

    public byte[] EventLogEventValueBinary { get; set; }

    public bool? EventLogEventValueBit { get; set; }

    public DateTime? EventLogEventValueDateTime { get; set; }

    public double? EventLogEventValueFloat { get; set; }

    public int? EventLogEventValueInt { get; set; }

    public string EventLogEventValueNtext { get; set; }

    public string EventLogEventValueVarChar { get; set; }

    public DateTime? ControlLogTime { get; set; }

    public byte[] ControlLogEventValueBinary { get; set; }

    public bool? ControlLogEventValueBit { get; set; }

    public DateTime? ControlLogEventValueDateTime { get; set; }

    public double? ControlLogEventValueFloat { get; set; }

    public int? ControlLogEventValueInt { get; set; }

    public string ControlLogEventValueNtext { get; set; }

    public string ControlLogEventValueVarChar { get; set; }
}