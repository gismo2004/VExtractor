﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnUserSessionLog
{
    public int Id { get; set; }

    public byte CompanyId { get; set; }

    public int UserId { get; set; }

    public DateTime LoginTime { get; set; }

    public DateTime? LogoutTime { get; set; }

    public string SessionId { get; set; }

    public string Ipaddress { get; set; }

    public bool EndedByUserInterAction { get; set; }
}