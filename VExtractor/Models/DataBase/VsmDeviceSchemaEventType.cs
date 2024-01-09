﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace VExtractor.Models.DataBase;

/// <summary>
/// Diese Tabelle definiert die Werte, die im Anlagenschema angezeigt werden.
/// </summary>
public partial class VsmDeviceSchemaEventType
{
    /// <summary>
    /// Id Feld des e-ControlNet aus der TAN Tabelle.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// CompanyId des Mandanten im e-ControlNet.
    /// </summary>
    public byte CompanyId { get; set; }

    /// <summary>
    /// Id des zugeordneten Anlagenschemas (vsmDeviceSchema).
    /// </summary>
    public int DeviceSchemaId { get; set; }

    /// <summary>
    /// DatapointId (Viessmann Regler Id) des e-ControlNet.
    /// </summary>
    public int DatapointId { get; set; }

    /// <summary>
    /// EventTypeId (Viessmann Datenpunkt Id) des e-ControlNet.
    /// </summary>
    public int EventTypeId { get; set; }

    /// <summary>
    /// Die X-Koordinate des Wertes innerhalb des Bildes in Pixeln ausgehend vom linken oberen Rand.
    /// </summary>
    public int PosX { get; set; }

    /// <summary>
    /// Die Y-Koordinate des Wertes innerhalb des Bildes in Pixeln ausgehend vom linken oberen Rand.
    /// </summary>
    public int PosY { get; set; }
}