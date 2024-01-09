﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnDataPoint
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public string Name { get; set; }

    public int DataPointTypeId { get; set; }

    public int DeviceId { get; set; }

    public string Address { get; set; }

    public string Description { get; set; }

    public string InformationDataSetXml { get; set; }

    public int StatusEventTypeId { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<EcnEventGroupMasterDataCache> EcnEventGroupMasterDataCaches { get; set; } = new List<EcnEventGroupMasterDataCache>();

    public virtual ICollection<EcnEventGroupValueCache> EcnEventGroupValueCaches { get; set; } = new List<EcnEventGroupValueCache>();

    public virtual ICollection<VsmEquipment> VsmEquipments { get; set; } = new List<VsmEquipment>();

    public virtual ICollection<VsmEventTypeConversion> VsmEventTypeConversions { get; set; } = new List<VsmEventTypeConversion>();

    public virtual ICollection<VsmOperationsDiary> VsmOperationsDiaries { get; set; } = new List<VsmOperationsDiary>();

    public virtual ICollection<VsmShoppingCart> VsmShoppingCarts { get; set; } = new List<VsmShoppingCart>();
}