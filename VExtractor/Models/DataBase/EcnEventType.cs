﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnEventType
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public bool EnumType { get; set; }

    public string Name { get; set; }

    public string Address { get; set; }

    public string Conversion { get; set; }

    public string Description { get; set; }

    public int Priority { get; set; }

    public bool Filtercriterion { get; set; }

    public bool Reportingcriterion { get; set; }

    public int Type { get; set; }

    public string Url { get; set; }

    public string DefaultValue { get; set; }

    public int ObjectId { get; set; }

    public virtual ICollection<VsmEventTypeConversion> VsmEventTypeConversions { get; set; } = new List<VsmEventTypeConversion>();

    public virtual VsmEventTypeExtension VsmEventTypeExtension { get; set; }
}