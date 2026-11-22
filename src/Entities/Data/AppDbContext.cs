using System;
using System.Collections.Generic;
using GoGoaRentals.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Entities.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingAssignment> BookingAssignments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationAttempt> NotificationAttempts { get; set; }

    public virtual DbSet<NotificationOutbox> NotificationOutboxes { get; set; }

    public virtual DbSet<NotificationTemplate> NotificationTemplates { get; set; }

    public virtual DbSet<PartnerServiceArea> PartnerServiceAreas { get; set; }

    public virtual DbSet<PartnerSettlement> PartnerSettlements { get; set; }

    public virtual DbSet<PartnerVehicle> PartnerVehicles { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; set; }

    public virtual DbSet<PickupLocation> PickupLocations { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<RentalPartner> RentalPartners { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleCalendarBlock> VehicleCalendarBlocks { get; set; }

    public virtual DbSet<VehicleCategory> VehicleCategories { get; set; }

    public virtual DbSet<VehicleDocument> VehicleDocuments { get; set; }

    public virtual DbSet<VehiclePricing> VehiclePricings { get; set; }

    public virtual DbSet<VehicleType> VehicleTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.Timestamp }, "IX_AuditLog_Entity").IsDescending(false, false, true);

            entity.HasIndex(e => e.Timestamp, "IX_AuditLog_Time").IsDescending();

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CorrelationId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.EntityId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK_AuditLog_Actor");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Booking");

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "IX_Booking_Customer").IsDescending(false, true);

            entity.HasIndex(e => new { e.BookingStatus, e.PickupAt }, "IX_Booking_Status");

            entity.HasIndex(e => new { e.PartnerVehicleId, e.PickupAt }, "IX_Booking_Vehicle").HasFilter("([PartnerVehicleId] IS NOT NULL)");

            entity.HasIndex(e => e.BookingNumber, "UQ_Booking_BookingNumber").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.IdempotencyKey }, "UQ_Booking_Idempotency").IsUnique();

            entity.Property(e => e.BookingNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancelledAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("INR")
                .IsFixedLength();
            entity.Property(e => e.CustomerPrice).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
            entity.Property(e => e.DeliveryFee).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.PickupAt).HasPrecision(0);
            entity.Property(e => e.ReturnAt).HasPrecision(0);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Customer).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Booking_Customer");

            entity.HasOne(d => d.PartnerVehicle).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PartnerVehicleId)
                .HasConstraintName("FK_Booking_PartnerVehicle");

            entity.HasOne(d => d.PickupLocation).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PickupLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Booking_PickupLocation");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.VehicleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Booking_VehicleType");
        });

        modelBuilder.Entity<BookingAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.ToTable("BookingAssignment");

            entity.HasIndex(e => new { e.RentalPartnerId, e.CreatedAt }, "IX_BookingAssignment_Partner").IsDescending(false, true);

            entity.HasIndex(e => new { e.BookingId, e.AttemptNumber }, "UQ_BookingAssignment_Attempt").IsUnique();

            entity.HasIndex(e => new { e.BookingId, e.AssignmentStatus }, "UX_BookingAssignment_OneReserved")
                .IsUnique()
                .HasFilter("([AssignmentStatus]=(2))");

            entity.Property(e => e.AssignedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.RankScore).HasColumnType("decimal(9, 4)");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingAssignments)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingAssignment_Booking");

            entity.HasOne(d => d.PartnerVehicle).WithMany(p => p.BookingAssignments)
                .HasForeignKey(d => d.PartnerVehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingAssignment_PartnerVehicle");

            entity.HasOne(d => d.RentalPartner).WithMany(p => p.BookingAssignments)
                .HasForeignKey(d => d.RentalPartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingAssignment_RentalPartner");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");

            entity.HasIndex(e => new { e.NotificationStatus, e.CreatedAt }, "IX_Notification_Failed").HasFilter("([NotificationStatus]=(5))");

            entity.HasIndex(e => new { e.RecipientUserId, e.CreatedAt }, "IX_Notification_Recipient").IsDescending(false, true);

            entity.HasIndex(e => e.IdempotencyKey, "UQ_Notification_Idempotency").IsUnique();

            entity.Property(e => e.Body).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.ProviderMessageId)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.RecipientAddress).HasMaxLength(256);
            entity.Property(e => e.SentAt).HasPrecision(0);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Booking).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK_Notification_Booking");

            entity.HasOne(d => d.RecipientUser).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Recipient");

            entity.HasOne(d => d.Template).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.TemplateId)
                .HasConstraintName("FK_Notification_Template");
        });

        modelBuilder.Entity<NotificationAttempt>(entity =>
        {
            entity.ToTable("NotificationAttempt");

            entity.HasIndex(e => new { e.NotificationId, e.AttemptNumber }, "UQ_NotificationAttempt").IsUnique();

            entity.Property(e => e.AttemptedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ProviderResponse).HasMaxLength(2000);

            entity.HasOne(d => d.Notification).WithMany(p => p.NotificationAttempts)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NotificationAttempt_Notification");
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.HasKey(e => e.OutboxId);

            entity.ToTable("NotificationOutbox");

            entity.HasIndex(e => new { e.ProcessingStatus, e.NextRetryAt }, "IX_NotificationOutbox_Due").HasFilter("([ProcessingStatus] IN ((1), (2)))");

            entity.HasIndex(e => e.EventId, "UQ_NotificationOutbox_EventId").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LockedUntil).HasPrecision(0);
            entity.Property(e => e.NextRetryAt).HasPrecision(0);
            entity.Property(e => e.ProcessedAt).HasPrecision(0);
            entity.Property(e => e.ProcessingStatus).HasDefaultValue((byte)1);
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId);

            entity.ToTable("NotificationTemplate");

            entity.HasIndex(e => new { e.EventType, e.Channel, e.Language }, "UX_NotificationTemplate_Active")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Language)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("en");
            entity.Property(e => e.ProviderTemplateName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Subject).HasMaxLength(200);
        });

        modelBuilder.Entity<PartnerServiceArea>(entity =>
        {
            entity.ToTable("PartnerServiceArea");

            entity.HasIndex(e => new { e.RentalPartnerId, e.PickupLocationId }, "UQ_PartnerServiceArea").IsUnique();

            entity.Property(e => e.DeliveryCost).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.PickupLocation).WithMany(p => p.PartnerServiceAreas)
                .HasForeignKey(d => d.PickupLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerServiceArea_PickupLocation");

            entity.HasOne(d => d.RentalPartner).WithMany(p => p.PartnerServiceAreas)
                .HasForeignKey(d => d.RentalPartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerServiceArea_RentalPartner");
        });

        modelBuilder.Entity<PartnerSettlement>(entity =>
        {
            entity.HasKey(e => e.SettlementId);

            entity.ToTable("PartnerSettlement");

            entity.HasIndex(e => new { e.RentalPartnerId, e.SettlementStatus }, "IX_PartnerSettlement_Partner");

            entity.HasIndex(e => e.BookingId, "UQ_PartnerSettlement_Booking").IsUnique();

            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeliveryCost).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.GrossAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.NetPayout).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.OtherDeductions).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.PaidAt).HasPrecision(0);

            entity.HasOne(d => d.Booking).WithOne(p => p.PartnerSettlement)
                .HasForeignKey<PartnerSettlement>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerSettlement_Booking");

            entity.HasOne(d => d.RentalPartner).WithMany(p => p.PartnerSettlements)
                .HasForeignKey(d => d.RentalPartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerSettlement_RentalPartner");
        });

        modelBuilder.Entity<PartnerVehicle>(entity =>
        {
            entity.ToTable("PartnerVehicle");

            entity.HasIndex(e => new { e.VehicleTypeId, e.Status, e.DocumentVerificationStatus }, "IX_PartnerVehicle_Type_Status");

            entity.HasIndex(e => e.VehicleId, "IX_PartnerVehicle_Vehicle");

            entity.HasIndex(e => e.RegistrationNumber, "UQ_PartnerVehicle_Registration").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeliveryRadiusKm).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.DocumentVerificationStatus).HasDefaultValue((byte)1);
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.RegistrationNumber)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status).HasDefaultValue((byte)1);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.PickupLocation).WithMany(p => p.PartnerVehicles)
                .HasForeignKey(d => d.PickupLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerVehicle_PickupLocation");

            entity.HasOne(d => d.RentalPartner).WithMany(p => p.PartnerVehicles)
                .HasForeignKey(d => d.RentalPartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerVehicle_RentalPartner");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.PartnerVehicles)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerVehicle_Vehicle");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.PartnerVehicles)
                .HasForeignKey(d => d.VehicleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnerVehicle_VehicleType");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment");

            entity.HasIndex(e => e.BookingId, "IX_Payment_Booking");

            entity.HasIndex(e => new { e.PaymentStatus, e.CreatedAt }, "IX_Payment_Status");

            entity.HasIndex(e => new { e.Gateway, e.GatewayOrderId }, "UQ_Payment_GatewayOrder").IsUnique();

            entity.HasIndex(e => e.IdempotencyKey, "UQ_Payment_Idempotency").IsUnique();

            entity.HasIndex(e => new { e.Gateway, e.GatewayPaymentId }, "UX_Payment_GatewayPayment")
                .IsUnique()
                .HasFilter("([GatewayPaymentId] IS NOT NULL)");

            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("INR")
                .IsFixedLength();
            entity.Property(e => e.Gateway).HasDefaultValue((byte)1);
            entity.Property(e => e.GatewayOrderId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.GatewayPaymentId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.PaidAt).HasPrecision(0);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Booking).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_Booking");
        });

        modelBuilder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.WebhookEventId);

            entity.ToTable("PaymentWebhookEvent");

            entity.HasIndex(e => new { e.Gateway, e.GatewayEventId }, "UQ_PaymentWebhookEvent").IsUnique();

            entity.Property(e => e.EventType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.GatewayEventId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ProcessedAt).HasPrecision(0);
            entity.Property(e => e.ReceivedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<PickupLocation>(entity =>
        {
            entity.ToTable("PickupLocation");

            entity.HasIndex(e => e.Name, "UQ_PickupLocation_Name").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CustomerDeliveryFee).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.ToTable("Refund");

            entity.HasIndex(e => e.IdempotencyKey, "UQ_Refund_Idempotency").IsUnique();

            entity.HasIndex(e => e.GatewayRefundId, "UX_Refund_GatewayRefund")
                .IsUnique()
                .HasFilter("([GatewayRefundId] IS NOT NULL)");

            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GatewayRefundId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Payment).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Refund_Payment");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.RequestedByUserId)
                .HasConstraintName("FK_Refund_RequestedBy");
        });

        modelBuilder.Entity<RentalPartner>(entity =>
        {
            entity.ToTable("RentalPartner");

            entity.HasIndex(e => e.UserId, "UQ_RentalPartner_UserId").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BusinessName).HasMaxLength(200);
            entity.Property(e => e.CommissionPercent)
                .HasDefaultValue(20.00m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ContactEmail).HasMaxLength(256);
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PartnerStatus).HasDefaultValue((byte)1);
            entity.Property(e => e.ReliabilityScore)
                .HasDefaultValue(80.00m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.RentalPartner)
                .HasForeignKey<RentalPartner>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RentalPartner_Users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.Name, "UQ_Role_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.Phone, "UX_Users_Phone")
                .IsUnique()
                .HasFilter("([Phone] IS NOT NULL)");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status).HasDefaultValue((byte)1);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.RoleNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.Role)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Role");
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Channel });

            entity.ToTable("UserNotificationPreference");

            entity.Property(e => e.ConsentGivenAt).HasPrecision(0);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.UserNotificationPreferences)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserNotificationPreference_Users");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicle");

            entity.HasIndex(e => e.VehicleTypeId, "IX_Vehicle_VehicleType");

            entity.HasIndex(e => new { e.Make, e.Model }, "UQ_Vehicle_MakeModel").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);

            entity.HasOne(d => d.VehicleType).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.VehicleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicle_VehicleType");
        });

        modelBuilder.Entity<VehicleCalendarBlock>(entity =>
        {
            entity.HasKey(e => e.CalendarBlockId);

            entity.ToTable("VehicleCalendarBlock");

            entity.HasIndex(e => e.ExpiresAt, "IX_VehicleCalendarBlock_Expiry").HasFilter("([Status]=(1) AND [ExpiresAt] IS NOT NULL)");

            entity.HasIndex(e => new { e.PartnerVehicleId, e.StartAt, e.EndAt }, "IX_VehicleCalendarBlock_Overlap").HasFilter("([Status] IN ((1), (4)))");

            entity.HasIndex(e => new { e.SourceBookingId, e.BlockType }, "UX_VehicleCalendarBlock_BookingActive")
                .IsUnique()
                .HasFilter("([SourceBookingId] IS NOT NULL AND ([Status] IN ((1), (4))))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.StartAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleCalendarBlocks)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleCalendarBlock_CreatedBy");

            entity.HasOne(d => d.PartnerVehicle).WithMany(p => p.VehicleCalendarBlocks)
                .HasForeignKey(d => d.PartnerVehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleCalendarBlock_PartnerVehicle");

            entity.HasOne(d => d.SourceBooking).WithMany(p => p.VehicleCalendarBlocks)
                .HasForeignKey(d => d.SourceBookingId)
                .HasConstraintName("FK_VehicleCalendarBlock_Booking");
        });

        modelBuilder.Entity<VehicleCategory>(entity =>
        {
            entity.ToTable("VehicleCategory");

            entity.HasIndex(e => e.Name, "UQ_VehicleCategory_Name").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<VehicleDocument>(entity =>
        {
            entity.ToTable("VehicleDocument");

            entity.HasIndex(e => new { e.PartnerVehicleId, e.DocumentType }, "IX_VehicleDocument_Vehicle");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.VerificationStatus).HasDefaultValue((byte)1);
            entity.Property(e => e.VerifiedAt).HasPrecision(0);

            entity.HasOne(d => d.PartnerVehicle).WithMany(p => p.VehicleDocuments)
                .HasForeignKey(d => d.PartnerVehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleDocument_PartnerVehicle");

            entity.HasOne(d => d.VerifiedByUser).WithMany(p => p.VehicleDocuments)
                .HasForeignKey(d => d.VerifiedByUserId)
                .HasConstraintName("FK_VehicleDocument_VerifiedBy");
        });

        modelBuilder.Entity<VehiclePricing>(entity =>
        {
            entity.HasKey(e => e.PricingId);

            entity.ToTable("VehiclePricing");

            entity.HasIndex(e => new { e.VehicleTypeId, e.DurationType, e.EffectiveFrom }, "IX_VehiclePricing_Lookup").HasFilter("([IsActive]=(1))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerPrice).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.EffectiveFrom).HasPrecision(0);
            entity.Property(e => e.EffectiveTo).HasPrecision(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TaxPercent).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.VehiclePricings)
                .HasForeignKey(d => d.VehicleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehiclePricing_VehicleType");
        });

        modelBuilder.Entity<VehicleType>(entity =>
        {
            entity.ToTable("VehicleType");

            entity.HasIndex(e => e.Name, "UQ_VehicleType_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status).HasDefaultValue((byte)1);

            entity.HasOne(d => d.VehicleCategory).WithMany(p => p.VehicleTypes)
                .HasForeignKey(d => d.VehicleCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleType_VehicleCategory");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
