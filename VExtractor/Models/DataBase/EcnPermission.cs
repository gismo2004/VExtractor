﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnPermission
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int? UserId { get; set; }

    public int? UserGroupId { get; set; }

    public int? ObjectId { get; set; }

    public int? ObjectGroupId { get; set; }

    public byte CanRead { get; set; }

    public byte CanWrite { get; set; }

    public byte CanModify { get; set; }

    public byte CanDelete { get; set; }

    public int Priority { get; set; }
}