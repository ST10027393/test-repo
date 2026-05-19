// FILE: TechMove_GLMS/Models/Contract.cs
﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using TechMove_GLMS.Patterns.Factory;
using TechMove_GLMS.Patterns.Observer;
using TechMove_GLMS.Patterns.State;

namespace TechMove_GLMS.Models;

public partial class Contract
{
    public int ContractId { get; set; }

    public int ClientId { get; set; }

    public string AssignedTo { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string ServiceLevel { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? SignedAgreementFilePath { get; set; }

    public virtual Client Client { get; set; } = null!;

    public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

    //GoF patterns
    // State pattern
    [NotMapped] //Sql will ignore this property as it's not mapped to the database
    private IContractState? _currentState;

    public void SetState(IContractState state)
    {
        _currentState = state;
    }

    // We now require the controller to hand us the REAL request
    public bool CanAcceptServiceRequest(ServiceRequest incomingRequest) 
    {
        // Dynamic State Resolution based on database string
        _currentState = Status switch
        {
            "Active" => new ActiveState(),
            "Expired" => new ExpiredState(),
            "On Hold" => new OnHoldState(),
            _ => new DraftState() // Default fallback for Draft
        };

        // Pass the REAL request down into your State Pattern interface
        return _currentState.HandleServiceRequest(this, incomingRequest);
    }

    // Observer pattern
    [NotMapped]
    private List<IContractObserver> _observers = new List<IContractObserver>();

    public void AttachObserver(IContractObserver observer)
    {
        _observers ??= new List<IContractObserver>();
        
        _observers.Add(observer);
    }

    public void ChangeAssignee(string newAssignee)
    {
        if (AssignedTo != newAssignee)
        {
            AssignedTo = newAssignee;
            NotifyObservers(); 
        }
    }

    public void ChangeStatus(string newStatus)
    {
        if (Status != newStatus)
        {
            Status = newStatus;
            
            _currentState = Status switch
            {
                "Active" => new ActiveState(),
                "Expired" => new ExpiredState(),
                _ => new DraftState() 
            };

            NotifyObservers(); 
        }
    }

    private void NotifyObservers()
    {
        if (_observers == null) return;

        foreach (var observer in _observers)
        {
            observer.Update(this);
        }
    }
}

public class ContractFilterDto
{
    // Hidden fields for Row-Level Security
    public string CurrentUserRole { get; set; } = null!;
    public string CurrentUserName { get; set; } = null!;
    
    // Filters from the UI
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public int? ClientId { get; set; } 
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string AssignedTo { get; set; } = null!;
}
