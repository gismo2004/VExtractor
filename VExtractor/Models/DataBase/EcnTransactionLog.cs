﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

public partial class EcnTransactionLog
{
    public byte CompanyId { get; set; }

    public long Id { get; set; }

    public int UserId { get; set; }

    public DateTime Time { get; set; }

    public string TableName { get; set; }

    public byte State { get; set; }

    public string OldValue { get; set; }

    public string NewValue { get; set; }
}