﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using VExtractor.Models.DataBase;

namespace VExtractor.Models.DataBase
{
    public partial class VDataBase
    {

        [DbFunction("DBVersion", "dbo")]
        public static string DBVersion()
        {
            throw new NotSupportedException("This method can only be called from Entity Framework Core queries");
        }

        protected void OnModelCreatingGeneratedFunctions(ModelBuilder modelBuilder)
        {
        }
    }
}