﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class VsmEventTypeExtension
{
    public int EventTypeId { get; set; }

    public byte CompanyId { get; set; }

    public string Viaaddress { get; set; }

    public bool? ViaconvertToString { get; set; }

    public int? CircuitTimeGroup { get; set; }

    public bool? QuickCheckEnabled { get; set; }

    public int? ByteLength { get; set; }

    public virtual EcnEventType EcnEventType { get; set; }
}