﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnTrendConfigChannel
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int OuId { get; set; }

    /// <summary>
    /// This field holds the link to table/field ecnTrendConfig.ID.
    /// </summary>
    public int TrendConfigId { get; set; }

    /// <summary>
    /// This fields holds the eventtypegroupID for the configured channel.
    /// </summary>
    public int GroupId { get; set; }

    public int DataPointId { get; set; }

    public int EventTypeId { get; set; }

    public int ChannelOrder { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? StopTime { get; set; }
}