// FILE: TechMove_GLMS/Models/ServiceRequest.cs
﻿using System;
using System.Collections.Generic;

namespace TechMove_GLMS.Models;

public partial class ServiceRequest
{
    public int RequestId { get; set; }

    public int ContractId { get; set; }

    public string AssignedTo { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal ForeignCost { get; set; }

    public string ForeignCurrencyCode { get; set; } = null!;

    public decimal LocalCostZar { get; set; }

    public string Status { get; set; } = null!;

    public virtual Contract Contract { get; set; } = null!;
}


public class ServiceRequestFilterDto
{
    public string CurrentUserRole { get; set; } = null!;
    public string CurrentUserName { get; set; } = null!;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public int? ContractId { get; set; }
    public int? ClientId { get; set; }
}