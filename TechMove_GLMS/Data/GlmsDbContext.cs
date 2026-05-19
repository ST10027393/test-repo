// FILE: TechMove_GLMS/Models/GlmsDbContext.cs
﻿using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Data;

public partial class GlmsDbContext : DbContext
{
    public GlmsDbContext()
    {
    }

    public GlmsDbContext(DbContextOptions<GlmsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Client> Clients { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ServiceRequest> ServiceRequests { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.ClientId).HasName("PK__Clients__E67E1A2437F8A81F");

            entity.Property(e => e.ContactDetails).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Region).HasMaxLength(100);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__Contract__C90D3469A0955569");

            entity.Property(e => e.ServiceLevel).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Client).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.ClientId)
                .HasConstraintName("FK_Contracts_Clients");
        });

        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__ServiceR__33A8517A5E8D6240");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ForeignCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ForeignCurrencyCode).HasMaxLength(3);
            entity.Property(e => e.LocalCostZar)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("LocalCostZAR");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Contract).WithMany(p => p.ServiceRequests)
                .HasForeignKey(d => d.ContractId)
                .HasConstraintName("FK_ServiceRequests_Contracts");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CF2B1C8B7");

            entity.HasIndex(e => e.FirebaseUid, "UQ__Users__F82B22B21005F992").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirebaseUid).HasMaxLength(128);
            entity.Property(e => e.Role).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
