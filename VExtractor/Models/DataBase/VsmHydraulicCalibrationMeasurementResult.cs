﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmHydraulicCalibrationMeasurementResult
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int ResultId { get; set; }

    public int MeasurementNumber { get; set; }

    public DateTime DateTime { get; set; }

    public int PressureDifference { get; set; }

    public int VolumeFlow { get; set; }

    public virtual ICollection<VsmHydraulicCalibrationMeasurementResultStatus> VsmHydraulicCalibrationMeasurementResultStatuses { get; set; } = new List<VsmHydraulicCalibrationMeasurementResultStatus>();

    public virtual VsmHydraulicCalibrationResult VsmHydraulicCalibrationResult { get; set; }
}