﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmDeviceImage
{
    public int DeviceId { get; set; }

    public byte CompanyId { get; set; }

    public string ImageUrl { get; set; }

    public virtual EcnCompany Company { get; set; }

    public virtual EcnDevice EcnDevice { get; set; }
}