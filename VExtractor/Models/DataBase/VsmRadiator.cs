﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmRadiator
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int RadiatorTypeId { get; set; }

    public int DimensionX { get; set; }

    public int DimensionY { get; set; }

    public int? DimensionZ { get; set; }

    public int NominalThermalOutput { get; set; }

    public virtual ICollection<VsmHydraulicCalibrationRadiatorConfig> VsmHydraulicCalibrationRadiatorConfigs { get; set; } = new List<VsmHydraulicCalibrationRadiatorConfig>();

    public virtual VsmRadiatorType VsmRadiatorType { get; set; }
}