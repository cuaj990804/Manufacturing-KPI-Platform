using GDIKPI.DTO;
using GDIKPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace GDIKPI.Data;

public partial class KpisContext : DbContext
{
    public KpisContext()
    {
    }

    public KpisContext(DbContextOptions<KpisContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Absenteeism> Absenteeisms { get; set; }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Break> Breaks { get; set; }

    public virtual DbSet<Command> Commands { get; set; }

    public virtual DbSet<Defect> Defects { get; set; }

    public virtual DbSet<DefectsCategory> DefectsCategories { get; set; }

    public virtual DbSet<DefectsDatum> DefectsData { get; set; }

    public virtual DbSet<DowntimeEvent> DowntimeEvents { get; set; }

    public virtual DbSet<Efficiency> Efficiencies { get; set; }

    public virtual DbSet<Eoopercentage> Eoopercentages { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<Oeehistory> Oeehistories { get; set; }

    public virtual DbSet<PerformanceMetric> PerformanceMetrics { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<ProductionDatum> ProductionData { get; set; }

    public virtual DbSet<ProductionLine> ProductionLines { get; set; }

    public virtual DbSet<ProductionOperator> ProductionOperators { get; set; }

    public virtual DbSet<ProductionOperatorsScan> ProductionOperatorsScans { get; set; }

    public virtual DbSet<Rejection> Rejections { get; set; }

    public virtual DbSet<ScannerProduction> ScannerProductions { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAreaPermission> UserAreaPermissions { get; set; }

    public virtual DbSet<UserLinePermission> UserLinePermissions { get; set; }

    public virtual DbSet<UserModulePermission> UserModulePermissions { get; set; }

    public virtual DbSet<VwDefectsManagement> VwDefectsManagements { get; set; }

    public virtual DbSet<VwOee> VwOees { get; set; }

    public virtual DbSet<VwProductionLine> VwProductionLines { get; set; }

    public virtual DbSet<VwReject> VwRejects { get; set; }

    public virtual DbSet<VwUserPermission> VwUserPermissions { get; set; }

    public virtual DbSet<DailyProductionDTO> DailyProduction { get; set; }

    public virtual DbSet<LineOEEDTO> LineOEE { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Absenteeism>(entity =>
        {
            entity.HasKey(e => e.AbsenteeismId).HasName("PK__Absentee__F4BB0A0FE261559D");

            entity.ToTable("Absenteeism");

            entity.Property(e => e.AbsenteeismId).HasColumnName("AbsenteeismID");
            entity.Property(e => e.AbsenteeismCategory).HasMaxLength(255);
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");

            entity.HasOne(d => d.EmployeeNumberNavigation).WithMany(p => p.Absenteeisms)
                .HasForeignKey(d => d.EmployeeNumber)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Absenteeism_Users");

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.Absenteeisms)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Absenteeism_ProductionLines");
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId).HasName("PK__Areas__70B820282CDF03D2");

            entity.Property(e => e.AreaId).HasColumnName("AreaID");
            entity.Property(e => e.AreaName).HasMaxLength(255);
            entity.Property(e => e.CustomerName).HasMaxLength(255);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CDD149700A7");

            entity.ToTable("AuditLog");

            entity.Property(e => e.AuditLogId).HasColumnName("AuditLogID");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EntityId).HasColumnName("EntityID");
            entity.Property(e => e.EntityName)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp).HasColumnType("datetime");
        });

        modelBuilder.Entity<Break>(entity =>
        {
            entity.HasKey(e => e.BreakId).HasName("PK__Breaks__B267A6199AE4AFFF");

            entity.Property(e => e.BreakId).HasColumnName("BreakID");
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.Breaks)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Breaks_ProductionLines");
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.HasIndex(e => new { e.Scope, e.CommandText }, "UQ_Commands_Scope_CommandText").IsUnique();

            entity.Property(e => e.ActionKey).HasMaxLength(50);
            entity.Property(e => e.CommandText).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Scope).HasMaxLength(50);
        });

        modelBuilder.Entity<Defect>(entity =>
        {
            entity.HasKey(e => e.DefectId).HasName("PK__Defects__144A37FC3727CB98");

            entity.Property(e => e.DefectId).HasColumnName("DefectID");
            entity.Property(e => e.AreaId).HasColumnName("AreaID");
            entity.Property(e => e.DefectCategoryId).HasColumnName("DefectCategoryID");
            entity.Property(e => e.DefectName).HasMaxLength(255);

            entity.HasOne(d => d.Area).WithMany(p => p.Defects)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Areas_Defects");

            entity.HasOne(d => d.DefectCategory).WithMany(p => p.Defects)
                .HasForeignKey(d => d.DefectCategoryId)
                .HasConstraintName("FK_Defects_DefectCategory");
        });

        modelBuilder.Entity<DefectsCategory>(entity =>
        {
            entity.HasKey(e => e.DefectCategoryId).HasName("PK__DefectsC__A2EC80E40E587172");

            entity.ToTable("DefectsCategory");

            entity.Property(e => e.DefectCategoryId).HasColumnName("DefectCategoryID");
            entity.Property(e => e.DefectCategoryName).HasMaxLength(255);
        });

        modelBuilder.Entity<DefectsDatum>(entity =>
        {
            entity.HasKey(e => e.DefectsDataId).HasName("PK__DefectsD__C8FE765BDB53273B");

            entity.Property(e => e.DefectsDataId).HasColumnName("DefectsDataID");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DefectId).HasColumnName("DefectID");
            entity.Property(e => e.DefectQuantity).HasDefaultValue(0);
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.RejectionId).HasColumnName("RejectionID");
            entity.Property(e => e.ScannerValue).HasMaxLength(50);
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Defect).WithMany(p => p.DefectsData)
                .HasForeignKey(d => d.DefectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DefectsData_Defects");

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.DefectsData)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProductionData_DefectData");

            entity.HasOne(d => d.Rejection).WithMany(p => p.DefectsData)
                .HasForeignKey(d => d.RejectionId)
                .HasConstraintName("Rejection_DefectData");
        });

        modelBuilder.Entity<DowntimeEvent>(entity =>
        {
            entity.HasKey(e => e.DowntimeId).HasName("PK__Downtime__99876E557A4FF821");

            entity.Property(e => e.DowntimeId).HasColumnName("DowntimeID");
            entity.Property(e => e.ClosedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DowntimeCategory).HasMaxLength(100);
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.OpenedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.StartTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.DowntimeEvents)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DowntimeEvents_ProductionLines");
        });

        modelBuilder.Entity<Efficiency>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Efficien__3214EC07E35FCC5C");

            entity.ToTable("Efficiency");

            entity.HasIndex(e => e.DateData, "IX_Efficiency_DateData");

            entity.HasIndex(e => e.ProductionLinesId, "IX_Efficiency_ProductionLinesID");

            entity.HasIndex(e => new { e.DateData, e.ProductionLinesId }, "UX_Efficiency_Date_Line").IsUnique();

            entity.Property(e => e.AreaCustomerName)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.EfficiencyPercentage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.HorasGanadas)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 4)");
            entity.Property(e => e.HorasUtilizadas)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 4)");
            entity.Property(e => e.LineNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.RegistrationDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TiempoEstandar).HasColumnType("decimal(12, 8)");
            entity.Property(e => e.TimeData).HasPrecision(0);

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.Efficiencies)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Efficiency_ProductionLines");
        });

        modelBuilder.Entity<Eoopercentage>(entity =>
        {
            entity.HasKey(e => e.EoopercentageId).HasName("PK__EOOPerce__62A3F9506662D5C3");

            entity.ToTable("EOOPercentage");

            entity.Property(e => e.EoopercentageId).HasColumnName("EOOPercentageID");
            entity.Property(e => e.Category)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaxValue).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.MetricName)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MinValue).HasColumnType("decimal(5, 2)");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleId).HasName("PK__Module__2B7477E7");

            entity.Property(e => e.ModuleId).HasColumnName("ModuleID");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModuleName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Route)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Oeehistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OEEHisto__3214EC077D646C37");

            entity.ToTable("OEEHistory");

            entity.Property(e => e.AreaCustomerName)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.AvailabilityPercentage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.LineNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OeePercentage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.PerformancePercentage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.QualityPercentage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.RegistrationDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<PerformanceMetric>(entity =>
        {
            entity.HasKey(e => e.MetricId).HasName("PK__Performa__56105645BB628B84");

            entity.Property(e => e.MetricId).HasColumnName("MetricID");
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MaxValue).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MetricName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MetricType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MinValue).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__Permissi__EFA6FB0F8A5120AC");

            entity.HasIndex(e => e.Name, "UQ__Permissi__737584F618110529").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ProductionDatum>(entity =>
        {
            entity.HasKey(e => e.ProductionId).HasName("PK__Producti__D5D9A2F533E0270F");

            entity.Property(e => e.ProductionId).HasColumnName("ProductionID");
            entity.Property(e => e.ProducedPieces).HasDefaultValue(0);
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.ProgramDescription)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RejectedPieces).HasDefaultValue(0);

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.ProductionData)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProductionLines_ProductionData");
        });

        modelBuilder.Entity<ProductionLine>(entity =>
        {
            entity.HasKey(e => e.ProductionLinesId).HasName("PK__Producti__FCE5C1C4B7F19DDD");

            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.AreaId).HasColumnName("AreaID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LineName).HasMaxLength(100);
            entity.Property(e => e.StandardTime).HasColumnType("decimal(18, 8)");

            entity.HasOne(d => d.Area).WithMany(p => p.ProductionLines)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("Areas_ProductionLines");

            entity.HasOne(d => d.ShiftNumberNavigation).WithMany(p => p.ProductionLines)
                .HasForeignKey(d => d.ShiftNumber)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Shifts_ProductionLines");
        });

        modelBuilder.Entity<ProductionOperator>(entity =>
        {
            entity.HasKey(e => e.OperatorId).HasName("PK__Producti__7BB12FAE57EC5B93");

            entity.HasIndex(e => e.EmployeeNumber, "IX_ProductionOperators_EmployeeNumber");

            entity.HasIndex(e => e.EmployeeNumber, "UQ__Producti__8D6635983BAD379F").IsUnique();

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.LastnameOperator)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NameOperator)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Operation)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Area).WithMany(p => p.ProductionOperators)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_ProductionOperators_Areas");
        });

        modelBuilder.Entity<ProductionOperatorsScan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producti__3214EC07FD65F58A");

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ScannedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Operator).WithMany(p => p.ProductionOperatorsScans)
                .HasForeignKey(d => d.OperatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductionScans_ProductionOperators");
        });

        modelBuilder.Entity<Rejection>(entity =>
        {
            entity.HasKey(e => e.RejectionId).HasName("PK__Rejectio__9CACD2B85617259C");

            entity.Property(e => e.RejectionId).HasColumnName("RejectionID");
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DefectQuantity).HasDefaultValue(0);
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.ProgramDescription)
                .HasMaxLength(255)
                .HasDefaultValue("Sin descripcion");
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.Rejections)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProductionLines_Rejections");
        });

        modelBuilder.Entity<ScannerProduction>(entity =>
        {
            entity.HasKey(e => e.ScannerProductionId).HasName("PK__ScannerP__942E4732A7760D72");

            entity.ToTable("ScannerProduction");

            entity.Property(e => e.ScannerProductionDateTime)
                .HasPrecision(0)
                .HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ScannerValue).HasMaxLength(500);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftNumber).HasName("PK__Shifts__C1E8BB153DE8AA73");

            entity.Property(e => e.ShiftNumber).ValueGeneratedNever();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.EmployeeNumber);

            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Area).HasMaxLength(50);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserAreaPermission>(entity =>
        {
            entity.HasKey(e => e.UserAreaPermissionId).HasName("PK__UserArea__3F6E3CEAEDCA2C97");

            entity.Property(e => e.UserAreaPermissionId).HasColumnName("UserAreaPermissionID");
            entity.Property(e => e.AreaId).HasColumnName("AreaID");
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");

            entity.HasOne(d => d.Area).WithMany(p => p.UserAreaPermissions)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserAreaPermissions_Areas");

            entity.HasOne(d => d.EmployeeNumberNavigation).WithMany(p => p.UserAreaPermissions)
                .HasForeignKey(d => d.EmployeeNumber)
                .HasConstraintName("UserAreaPermissions_Users");

            entity.HasOne(d => d.Permission).WithMany(p => p.UserAreaPermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserAreaPermissions_Permissions");
        });

        modelBuilder.Entity<UserLinePermission>(entity =>
        {
            entity.HasKey(e => e.UserLinePermissionId).HasName("PK__UserLine__8CB5683F15628EE6");

            entity.Property(e => e.UserLinePermissionId).HasColumnName("UserLinePermissionID");
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");

            entity.HasOne(d => d.EmployeeNumberNavigation).WithMany(p => p.UserLinePermissions)
                .HasForeignKey(d => d.EmployeeNumber)
                .HasConstraintName("UserLinePermissions_Users");

            entity.HasOne(d => d.Permission).WithMany(p => p.UserLinePermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserLinePermissions_Permissions");

            entity.HasOne(d => d.ProductionLines).WithMany(p => p.UserLinePermissions)
                .HasForeignKey(d => d.ProductionLinesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserLinePermissions_Lines");
        });

        modelBuilder.Entity<UserModulePermission>(entity =>
        {
            entity.HasKey(e => e.UserModulePermissionId).HasName("PK__UserModule__3F6E3CEA");

            entity.Property(e => e.UserModulePermissionId).HasColumnName("UserModulePermissionID");
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ModuleId).HasColumnName("ModuleID");
            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");

            entity.HasOne(d => d.EmployeeNumberNavigation).WithMany(p => p.UserModulePermissions)
                .HasForeignKey(d => d.EmployeeNumber)
                .HasConstraintName("UserModulePermissions_Users");

            entity.HasOne(d => d.Module).WithMany(p => p.UserModulePermissions)
                .HasForeignKey(d => d.ModuleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserModulePermissions_Modules");

            entity.HasOne(d => d.Permission).WithMany(p => p.UserModulePermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserModulePermissions_Permissions");
        });

        modelBuilder.Entity<VwDefectsManagement>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_DefectsManagement");

            entity.Property(e => e.CustomerArea).HasMaxLength(513);
            entity.Property(e => e.DefectCategoryName).HasMaxLength(255);
            entity.Property(e => e.DefectName).HasMaxLength(255);
        });

        modelBuilder.Entity<VwOee>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OEE");

            entity.Property(e => e.Area).HasMaxLength(513);
            entity.Property(e => e.Category).HasMaxLength(255);
            entity.Property(e => e.ProductionLinesId).HasColumnName("ProductionLinesID");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwProductionLine>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ProductionLines");

            entity.Property(e => e.AreaName).HasMaxLength(255);
            entity.Property(e => e.CustomerName).HasMaxLength(255);
            entity.Property(e => e.Descansos)
                .HasMaxLength(8000)
                .IsUnicode(false);
            entity.Property(e => e.SupervisorName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwReject>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_Rejects");

            entity.Property(e => e.Area).HasMaxLength(513);
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DefectName).HasMaxLength(255);
            entity.Property(e => e.ProgramDescription).HasMaxLength(255);
        });

        modelBuilder.Entity<VwUserPermission>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_UserPermissions");

            entity.Property(e => e.AccessType)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Area).HasMaxLength(513);
            entity.Property(e => e.AreaId).HasColumnName("AreaID");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Permissions)
                .HasMaxLength(8000)
                .IsUnicode(false);
        });
        modelBuilder.Entity<DailyProductionDTO>(entity =>
        {
            entity.HasNoKey();
        });

        modelBuilder.Entity<LineOEEDTO>(entity =>
        {
            entity.HasNoKey();
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
