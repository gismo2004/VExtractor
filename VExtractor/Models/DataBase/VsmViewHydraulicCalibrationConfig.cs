﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmViewHydraulicCalibrationConfig
{
    public byte CompanyId { get; set; }

    public int ConfigId { get; set; }

    public int DeviceId { get; set; }

    public int DatapointId { get; set; }

    public int HeatingCircuitId { get; set; }

    public int RoomId { get; set; }

    public int RoomNumber { get; set; }

    public string RoomName { get; set; }

    public int RoomHeatingDemand { get; set; }

    public int? RadiatorConfigId { get; set; }

    public int? RadiatorId { get; set; }

    public int? ValveId { get; set; }

    public int? ActuatorId { get; set; }

    public double? RadiatorRequiredVolumeFlow { get; set; }

    public int? RadiatorNumber { get; set; }

    public int? RadiatorTypeId { get; set; }

    public int? RadiatorDimensionX { get; set; }

    public int? RadiatorDimensionY { get; set; }

    public int? RadiatorDimensionZ { get; set; }

    public int? RadiatorNominalThermalOutput { get; set; }

    public string RadiatorTypeName { get; set; }

    public string RadiatorTypeDescription { get; set; }

    public string RadiatorDimensionXunit { get; set; }

    public string RadiatorDimensionYunit { get; set; }

    public string RadiatorDimensionZunit { get; set; }

    public bool? RadiatorUseDimensionZ { get; set; }

    public string RadiatorDimensionXname { get; set; }

    public string RadiatorDimensionYname { get; set; }

    public string RadiatorDimensionZname { get; set; }

    public double ClimateZone { get; set; }

    public double Niveau { get; set; }

    public int Category { get; set; }

    public int? Type { get; set; }

    public int? Pump { get; set; }

    public string PartNo { get; set; }

    public bool IsRecalculated { get; set; }
}