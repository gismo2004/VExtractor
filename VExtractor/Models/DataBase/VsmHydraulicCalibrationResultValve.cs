﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmHydraulicCalibrationResultValve
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int RadiatorConfigId { get; set; }

    public int ResultId { get; set; }

    public int ValveNumber { get; set; }

    public double PressureLoss { get; set; }

    public string DefaultSetting { get; set; }

    public virtual ICollection<VsmHydraulicCalibrationMeasurementResultStatus> VsmHydraulicCalibrationMeasurementResultStatuses { get; set; } = new List<VsmHydraulicCalibrationMeasurementResultStatus>();

    public virtual VsmHydraulicCalibrationRadiatorConfig VsmHydraulicCalibrationRadiatorConfig { get; set; }

    public virtual VsmHydraulicCalibrationResult VsmHydraulicCalibrationResult { get; set; }
}