﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnDisplayConditionGroup
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public string Name { get; set; }

    public byte Type { get; set; }

    public int ParentId { get; set; }

    public string Description { get; set; }

    public int EventTypeIdDest { get; set; }

    public int EventTypeGroupIdDest { get; set; }
}