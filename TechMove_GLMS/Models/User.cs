// FILE: TechMove_GLMS/Models/User.cs
﻿using System;
using System.Collections.Generic;

namespace TechMove_GLMS.Models;

public partial class User
{
    public int UserId { get; set; }

    public string FirebaseUid { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }
}
