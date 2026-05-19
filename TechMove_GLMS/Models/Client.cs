// FILE: TechMove_GLMS/Models/Client.cs
﻿using System;
using System.Collections.Generic;

namespace TechMove_GLMS.Models;

public partial class Client
{
    public int ClientId { get; set; }

    public string Name { get; set; } = null!;

    public string ContactDetails { get; set; } = null!;

    public string Region { get; set; } = null!;

    // Separated currency code
    public string CurrencyCode { get; set; } = null!;
    
    // Row-level security assignment
    public string AssignedTo { get; set; } = null!;
    
    // For date filtering
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

// DTO for filtering clients in the service layer, includes hidden fields for security enforcement
public class ClientFilterDto
{
    public string SearchTerm { get; set; } = null!;
    public string Region { get; set; } = null!;
    public DateTime? DateCreated { get; set; }
    public string AssignedTo { get; set; } = null!;
    
    // Hidden fields used by ICClientService to enforce security
    public string CurrentUserRole { get; set; } = null!;
    public string CurrentUserName { get; set; } = null!;
}
