﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnDeviceSchedule
{
    public byte CompanyId { get; set; }

    public int DeviceId { get; set; }

    public DateTime NextRun { get; set; }

    public DateTime? LastRun { get; set; }

    public bool InProgress { get; set; }

    public int ActionTypeId { get; set; }

    public int WorkState { get; set; }
}