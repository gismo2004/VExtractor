﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmDatapointTypeGroupDatapointTypeLink
{
    public byte CompanyId { get; set; }

    public int DatapointTypeGroupId { get; set; }

    public int DatapointTypeId { get; set; }

    public virtual EcnDatapointType EcnDatapointType { get; set; }

    public virtual VsmDatapointTypeGroup VsmDatapointTypeGroup { get; set; }
}