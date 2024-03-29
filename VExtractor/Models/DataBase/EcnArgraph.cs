﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnArgraph
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public long TimeSpan { get; set; }

    public bool TimeSpanFixed { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int? Scale { get; set; }

    public long? ScaleResolution { get; set; }

    public string ChartType { get; set; }

    public int ObjectId { get; set; }

    public int? DynamicStartYearOffset { get; set; }

    public int? DynamicStartMonthOffset { get; set; }

    public int? DynamicStartDayOffest { get; set; }

    public long? DynamicStartTimeOffest { get; set; }

    public int? DynamicStopYearOffset { get; set; }

    public int? DynamicStopMonthOffset { get; set; }

    public int? DynamicStopDayOffest { get; set; }

    public long? DynamicStopTimeOffest { get; set; }

    public bool FixedTime { get; set; }
}