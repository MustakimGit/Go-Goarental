/*
    GoGoaRentals - initial schema (Phase 1)
    Target: SQL Server 2019+ / LocalDB. Run in SSMS against an empty database named GoGoaRentals.

    Conventions
    - All instants are stored as UTC in datetime2(0). Daily price boundaries are evaluated in IST (Asia/Kolkata) in code.
    - Time intervals are start-inclusive, end-exclusive: [StartAt, EndAt).
    - Money: decimal(12,2) in INR. Razorpay amounts (paise) are converted at the gateway boundary only.
    - Stable lifecycle states are TINYINT columns guarded by CHECK constraints; each maps 1:1 to a C# enum
      in src/Entities/Enums (see docs/SCAFFOLDING.md). Adding a value = add enum member + ALTER the CHECK.
    - Admin-managed values (vehicle categories, pickup locations, pricing) are tables, not enums.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- required for filtered indexes (sqlcmd defaults this OFF)
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* ---------------------------------------------------------------- Identity */

CREATE TABLE dbo.Users
(
    UserId          INT IDENTITY(1,1)  NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Name            NVARCHAR(150)      NOT NULL,
    Email           NVARCHAR(256)      NOT NULL,
    Phone           VARCHAR(20)        NULL,
    PasswordHash    NVARCHAR(500)      NOT NULL,
    Role            TINYINT            NOT NULL,   -- UserRole: 1 Customer, 2 RentalPartner, 3 SuperAdmin
    Status          TINYINT            NOT NULL CONSTRAINT DF_Users_Status DEFAULT (1), -- UserStatus: 1 Active, 2 Suspended, 3 Deleted
    CreatedAt       DATETIME2(0)       NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2(0)       NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role   CHECK (Role IN (1,2,3)),
    CONSTRAINT CK_Users_Status CHECK (Status IN (1,2,3))
);
CREATE UNIQUE INDEX UX_Users_Phone ON dbo.Users (Phone) WHERE Phone IS NOT NULL;

CREATE TABLE dbo.UserNotificationPreference
(
    UserId              INT          NOT NULL,
    Channel             TINYINT      NOT NULL,   -- NotificationChannel: 1 Email, 2 Sms, 3 WhatsApp, 4 InApp, 5 WebPush
    IsEnabled           BIT          NOT NULL,
    ConsentGivenAt      DATETIME2(0) NULL,       -- required for WhatsApp
    UpdatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_UserNotificationPreference_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserNotificationPreference PRIMARY KEY (UserId, Channel),
    CONSTRAINT FK_UserNotificationPreference_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_UserNotificationPreference_Channel CHECK (Channel IN (1,2,3,4,5))
);

/* ---------------------------------------------------------------- Locations */

CREATE TABLE dbo.PickupLocation
(
    PickupLocationId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PickupLocation PRIMARY KEY,
    Name                NVARCHAR(150)     NOT NULL,
    Address             NVARCHAR(500)     NOT NULL,
    Latitude            DECIMAL(9,6)      NOT NULL,
    Longitude           DECIMAL(9,6)      NOT NULL,
    CustomerDeliveryFee DECIMAL(12,2)     NOT NULL CONSTRAINT DF_PickupLocation_CustomerDeliveryFee DEFAULT (0), -- public fee charged to customer
    IsActive            BIT               NOT NULL CONSTRAINT DF_PickupLocation_IsActive DEFAULT (1),
    CONSTRAINT UQ_PickupLocation_Name UNIQUE (Name),
    CONSTRAINT CK_PickupLocation_Latitude  CHECK (Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_PickupLocation_Longitude CHECK (Longitude BETWEEN -180 AND 180),
    CONSTRAINT CK_PickupLocation_Fee CHECK (CustomerDeliveryFee >= 0)
);

/* ---------------------------------------------------------------- Partners */

CREATE TABLE dbo.RentalPartner
(
    RentalPartnerId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RentalPartner PRIMARY KEY,
    UserId              INT               NOT NULL,
    BusinessName        NVARCHAR(200)     NOT NULL,
    ContactPhone        VARCHAR(20)       NOT NULL,
    ContactEmail        NVARCHAR(256)     NULL,
    Address             NVARCHAR(500)     NULL,
    PartnerStatus       TINYINT           NOT NULL CONSTRAINT DF_RentalPartner_Status DEFAULT (1), -- PartnerStatus: 1 PendingApproval, 2 Active, 3 Suspended, 4 Rejected
    ReliabilityScore    DECIMAL(5,2)      NOT NULL CONSTRAINT DF_RentalPartner_Reliability DEFAULT (80.00), -- 0..100
    CommissionPercent   DECIMAL(5,2)      NOT NULL CONSTRAINT DF_RentalPartner_Commission DEFAULT (20.00),
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_RentalPartner_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_RentalPartner_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_RentalPartner_UserId UNIQUE (UserId),
    CONSTRAINT FK_RentalPartner_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_RentalPartner_Status CHECK (PartnerStatus IN (1,2,3,4)),
    CONSTRAINT CK_RentalPartner_Reliability CHECK (ReliabilityScore BETWEEN 0 AND 100),
    CONSTRAINT CK_RentalPartner_Commission CHECK (CommissionPercent BETWEEN 0 AND 100)
);

CREATE TABLE dbo.PartnerServiceArea
(
    PartnerServiceAreaId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartnerServiceArea PRIMARY KEY,
    RentalPartnerId      INT               NOT NULL,
    PickupLocationId     INT               NOT NULL,
    DeliveryAvailable    BIT               NOT NULL CONSTRAINT DF_PartnerServiceArea_Delivery DEFAULT (0),
    DeliveryCost         DECIMAL(12,2)     NOT NULL CONSTRAINT DF_PartnerServiceArea_DeliveryCost DEFAULT (0), -- partner's cost, used in ranking & settlement
    IsActive             BIT               NOT NULL CONSTRAINT DF_PartnerServiceArea_IsActive DEFAULT (1),
    CONSTRAINT UQ_PartnerServiceArea UNIQUE (RentalPartnerId, PickupLocationId),
    CONSTRAINT FK_PartnerServiceArea_RentalPartner  FOREIGN KEY (RentalPartnerId)  REFERENCES dbo.RentalPartner (RentalPartnerId),
    CONSTRAINT FK_PartnerServiceArea_PickupLocation FOREIGN KEY (PickupLocationId) REFERENCES dbo.PickupLocation (PickupLocationId),
    CONSTRAINT CK_PartnerServiceArea_DeliveryCost CHECK (DeliveryCost >= 0)
);

/* ---------------------------------------------------------------- Vehicles */

CREATE TABLE dbo.VehicleCategory   -- admin-managed lookup (TwoWheeler, FourWheeler, ...)
(
    VehicleCategoryId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleCategory PRIMARY KEY,
    Name                NVARCHAR(100)     NOT NULL,
    IsActive            BIT               NOT NULL CONSTRAINT DF_VehicleCategory_IsActive DEFAULT (1),
    CONSTRAINT UQ_VehicleCategory_Name UNIQUE (Name)
);

CREATE TABLE dbo.VehicleType       -- what the customer books, e.g. "Scooter (Activa class)", "SUV"
(
    VehicleTypeId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleType PRIMARY KEY,
    VehicleCategoryId   INT               NOT NULL,
    Name                NVARCHAR(150)     NOT NULL,
    Description         NVARCHAR(1000)    NULL,
    SeatingCapacity     TINYINT           NULL,
    ImageUrl            NVARCHAR(500)     NULL,
    Status              TINYINT           NOT NULL CONSTRAINT DF_VehicleType_Status DEFAULT (1), -- VehicleTypeStatus: 1 Active, 2 Inactive
    CONSTRAINT UQ_VehicleType_Name UNIQUE (Name),
    CONSTRAINT FK_VehicleType_VehicleCategory FOREIGN KEY (VehicleCategoryId) REFERENCES dbo.VehicleCategory (VehicleCategoryId),
    CONSTRAINT CK_VehicleType_Status CHECK (Status IN (1,2))
);

CREATE TABLE dbo.PartnerVehicle    -- one physical vehicle
(
    PartnerVehicleId            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartnerVehicle PRIMARY KEY,
    RentalPartnerId             INT               NOT NULL,
    VehicleTypeId               INT               NOT NULL,
    RegistrationNumber          VARCHAR(15)       NOT NULL,  -- normalized in code: uppercase, no spaces/hyphens (e.g. GA07AB1234)
    Make                        NVARCHAR(100)     NOT NULL,
    Model                       NVARCHAR(100)     NOT NULL,
    ManufacturingYear           SMALLINT          NOT NULL,
    Status                      TINYINT           NOT NULL CONSTRAINT DF_PartnerVehicle_Status DEFAULT (1), -- VehicleStatus: 1 PendingVerification, 2 Active, 3 Inactive, 4 Retired
    PickupLocationId            INT               NOT NULL,  -- home base
    DeliveryRadiusKm            DECIMAL(6,2)      NOT NULL CONSTRAINT DF_PartnerVehicle_Radius DEFAULT (0),
    DocumentVerificationStatus  TINYINT           NOT NULL CONSTRAINT DF_PartnerVehicle_DocStatus DEFAULT (1), -- DocumentVerificationStatus: 1 Pending, 2 Verified, 3 Rejected, 4 Expired
    CreatedAt                   DATETIME2(0)      NOT NULL CONSTRAINT DF_PartnerVehicle_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt                   DATETIME2(0)      NOT NULL CONSTRAINT DF_PartnerVehicle_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_PartnerVehicle_Registration UNIQUE (RegistrationNumber),
    CONSTRAINT FK_PartnerVehicle_RentalPartner  FOREIGN KEY (RentalPartnerId)  REFERENCES dbo.RentalPartner (RentalPartnerId),
    CONSTRAINT FK_PartnerVehicle_VehicleType    FOREIGN KEY (VehicleTypeId)    REFERENCES dbo.VehicleType (VehicleTypeId),
    CONSTRAINT FK_PartnerVehicle_PickupLocation FOREIGN KEY (PickupLocationId) REFERENCES dbo.PickupLocation (PickupLocationId),
    CONSTRAINT CK_PartnerVehicle_Status    CHECK (Status IN (1,2,3,4)),
    CONSTRAINT CK_PartnerVehicle_DocStatus CHECK (DocumentVerificationStatus IN (1,2,3,4)),
    CONSTRAINT CK_PartnerVehicle_Year      CHECK (ManufacturingYear BETWEEN 1990 AND 2100),
    CONSTRAINT CK_PartnerVehicle_Radius    CHECK (DeliveryRadiusKm >= 0),
    CONSTRAINT CK_PartnerVehicle_RegFormat CHECK (RegistrationNumber NOT LIKE '%[^A-Z0-9]%')
);
CREATE INDEX IX_PartnerVehicle_Type_Status ON dbo.PartnerVehicle (VehicleTypeId, Status, DocumentVerificationStatus) INCLUDE (RentalPartnerId, PickupLocationId);

CREATE TABLE dbo.VehicleDocument   -- RC, insurance, PUC, permit
(
    VehicleDocumentId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleDocument PRIMARY KEY,
    PartnerVehicleId    INT               NOT NULL,
    DocumentType        TINYINT           NOT NULL,  -- VehicleDocumentType: 1 RegistrationCertificate, 2 Insurance, 3 PollutionCertificate, 4 RentalPermit
    FileUrl             NVARCHAR(500)     NOT NULL,
    ExpiresOn           DATE              NULL,
    VerificationStatus  TINYINT           NOT NULL CONSTRAINT DF_VehicleDocument_Status DEFAULT (1), -- DocumentVerificationStatus
    VerifiedByUserId    INT               NULL,
    VerifiedAt          DATETIME2(0)      NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_VehicleDocument_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_VehicleDocument_PartnerVehicle FOREIGN KEY (PartnerVehicleId) REFERENCES dbo.PartnerVehicle (PartnerVehicleId),
    CONSTRAINT FK_VehicleDocument_VerifiedBy     FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_VehicleDocument_Type   CHECK (DocumentType IN (1,2,3,4)),
    CONSTRAINT CK_VehicleDocument_Status CHECK (VerificationStatus IN (1,2,3,4))
);
CREATE INDEX IX_VehicleDocument_Vehicle ON dbo.VehicleDocument (PartnerVehicleId, DocumentType);

/* ---------------------------------------------------------------- Pricing */

CREATE TABLE dbo.VehiclePricing
(
    PricingId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehiclePricing PRIMARY KEY,
    VehicleTypeId   INT               NOT NULL,
    DurationType    TINYINT           NOT NULL,   -- DurationType: 1 Hourly, 2 Daily
    CustomerPrice   DECIMAL(12,2)     NOT NULL,
    DepositAmount   DECIMAL(12,2)     NOT NULL CONSTRAINT DF_VehiclePricing_Deposit DEFAULT (0),
    TaxPercent      DECIMAL(5,2)      NOT NULL CONSTRAINT DF_VehiclePricing_Tax DEFAULT (0),
    EffectiveFrom   DATETIME2(0)      NOT NULL,
    EffectiveTo     DATETIME2(0)      NULL,       -- exclusive; NULL = open-ended
    IsActive        BIT               NOT NULL CONSTRAINT DF_VehiclePricing_IsActive DEFAULT (1),
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_VehiclePricing_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_VehiclePricing_VehicleType FOREIGN KEY (VehicleTypeId) REFERENCES dbo.VehicleType (VehicleTypeId),
    CONSTRAINT CK_VehiclePricing_DurationType CHECK (DurationType IN (1,2)),
    CONSTRAINT CK_VehiclePricing_Price   CHECK (CustomerPrice > 0),
    CONSTRAINT CK_VehiclePricing_Deposit CHECK (DepositAmount >= 0),
    CONSTRAINT CK_VehiclePricing_Tax     CHECK (TaxPercent BETWEEN 0 AND 100),
    CONSTRAINT CK_VehiclePricing_Range   CHECK (EffectiveTo IS NULL OR EffectiveTo > EffectiveFrom)
);
-- Overlapping active price windows for the same type+duration are rejected by the pricing service under an app lock.
CREATE INDEX IX_VehiclePricing_Lookup ON dbo.VehiclePricing (VehicleTypeId, DurationType, EffectiveFrom) WHERE IsActive = 1;

/* ---------------------------------------------------------------- Bookings */

CREATE TABLE dbo.Booking
(
    BookingId           INT IDENTITY(1,1)  NOT NULL CONSTRAINT PK_Booking PRIMARY KEY,
    BookingNumber       VARCHAR(20)        NOT NULL,  -- e.g. GR-20261027-000123
    CustomerId          INT                NOT NULL,
    VehicleTypeId       INT                NOT NULL,
    PartnerVehicleId    INT                NULL,      -- set when a reservation is committed
    PickupLocationId    INT                NOT NULL,
    PickupMode          TINYINT            NOT NULL,  -- PickupMode: 1 SelfPickup, 2 Delivery
    DeliveryAddress     NVARCHAR(500)      NULL,
    PickupAt            DATETIME2(0)       NOT NULL,
    ReturnAt            DATETIME2(0)       NOT NULL,
    BookingStatus       TINYINT            NOT NULL,  -- BookingStatus: 1 Pending, 2 AllocationInProgress, 3 AwaitingPayment, 4 Confirmed, 5 InProgress, 6 Completed, 7 CancellationRequested, 8 Cancelled, 9 Failed
    CustomerPrice       DECIMAL(12,2)      NOT NULL,
    DeliveryFee         DECIMAL(12,2)      NOT NULL CONSTRAINT DF_Booking_DeliveryFee DEFAULT (0),
    DepositAmount       DECIMAL(12,2)      NOT NULL CONSTRAINT DF_Booking_Deposit DEFAULT (0),
    TaxAmount           DECIMAL(12,2)      NOT NULL CONSTRAINT DF_Booking_Tax DEFAULT (0),
    TotalAmount         DECIMAL(12,2)      NOT NULL,
    Currency            CHAR(3)            NOT NULL CONSTRAINT DF_Booking_Currency DEFAULT ('INR'),
    PricingSnapshot     NVARCHAR(MAX)      NOT NULL,  -- JSON: price ids, units, rates, fees, taxes at booking time
    IdempotencyKey      VARCHAR(64)        NOT NULL,
    CancellationReason  TINYINT            NULL,      -- CancellationReason
    CancelledAt         DATETIME2(0)       NULL,
    CreatedAt           DATETIME2(0)       NOT NULL CONSTRAINT DF_Booking_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0)       NOT NULL CONSTRAINT DF_Booking_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    RowVersion          ROWVERSION         NOT NULL,  -- optimistic concurrency for state transitions
    CONSTRAINT UQ_Booking_BookingNumber UNIQUE (BookingNumber),
    CONSTRAINT UQ_Booking_Idempotency   UNIQUE (CustomerId, IdempotencyKey),
    CONSTRAINT FK_Booking_Customer       FOREIGN KEY (CustomerId)       REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Booking_VehicleType    FOREIGN KEY (VehicleTypeId)    REFERENCES dbo.VehicleType (VehicleTypeId),
    CONSTRAINT FK_Booking_PartnerVehicle FOREIGN KEY (PartnerVehicleId) REFERENCES dbo.PartnerVehicle (PartnerVehicleId),
    CONSTRAINT FK_Booking_PickupLocation FOREIGN KEY (PickupLocationId) REFERENCES dbo.PickupLocation (PickupLocationId),
    CONSTRAINT CK_Booking_Interval   CHECK (ReturnAt > PickupAt),
    CONSTRAINT CK_Booking_Status     CHECK (BookingStatus IN (1,2,3,4,5,6,7,8,9)),
    CONSTRAINT CK_Booking_PickupMode CHECK (PickupMode IN (1,2)),
    CONSTRAINT CK_Booking_Delivery   CHECK (PickupMode = 1 OR DeliveryAddress IS NOT NULL),
    CONSTRAINT CK_Booking_CancelReason CHECK (CancellationReason IS NULL OR CancellationReason IN (1,2,3,4,5,6)),
    CONSTRAINT CK_Booking_Amounts    CHECK (CustomerPrice >= 0 AND DeliveryFee >= 0 AND DepositAmount >= 0 AND TaxAmount >= 0
                                            AND TotalAmount = CustomerPrice + DeliveryFee + DepositAmount + TaxAmount),
    CONSTRAINT CK_Booking_Currency   CHECK (Currency = 'INR')
);
CREATE INDEX IX_Booking_Customer ON dbo.Booking (CustomerId, CreatedAt DESC);
CREATE INDEX IX_Booking_Vehicle  ON dbo.Booking (PartnerVehicleId, PickupAt) WHERE PartnerVehicleId IS NOT NULL;
CREATE INDEX IX_Booking_Status   ON dbo.Booking (BookingStatus, PickupAt);

CREATE TABLE dbo.BookingAssignment
(
    AssignmentId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingAssignment PRIMARY KEY,
    BookingId           INT               NOT NULL,
    PartnerVehicleId    INT               NOT NULL,
    RentalPartnerId     INT               NOT NULL,
    AttemptNumber       SMALLINT          NOT NULL,
    AssignmentStatus    TINYINT           NOT NULL,  -- BookingAssignmentStatus: 1 Candidate, 2 Reserved, 3 Failed, 4 Released, 5 Reassigned
    RankScore           DECIMAL(9,4)      NULL,
    AssignedAt          DATETIME2(0)      NULL,
    FailureReason       NVARCHAR(500)     NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_BookingAssignment_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_BookingAssignment_Attempt UNIQUE (BookingId, AttemptNumber),
    CONSTRAINT FK_BookingAssignment_Booking        FOREIGN KEY (BookingId)        REFERENCES dbo.Booking (BookingId),
    CONSTRAINT FK_BookingAssignment_PartnerVehicle FOREIGN KEY (PartnerVehicleId) REFERENCES dbo.PartnerVehicle (PartnerVehicleId),
    CONSTRAINT FK_BookingAssignment_RentalPartner  FOREIGN KEY (RentalPartnerId)  REFERENCES dbo.RentalPartner (RentalPartnerId),
    CONSTRAINT CK_BookingAssignment_Status CHECK (AssignmentStatus IN (1,2,3,4,5))
);
-- At most one live reservation per booking.
CREATE UNIQUE INDEX UX_BookingAssignment_OneReserved ON dbo.BookingAssignment (BookingId) WHERE AssignmentStatus = 2;
CREATE INDEX IX_BookingAssignment_Partner ON dbo.BookingAssignment (RentalPartnerId, CreatedAt DESC);

CREATE TABLE dbo.VehicleCalendarBlock
(
    CalendarBlockId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleCalendarBlock PRIMARY KEY,
    PartnerVehicleId    INT               NOT NULL,
    StartAt             DATETIME2(0)      NOT NULL,  -- inclusive
    EndAt               DATETIME2(0)      NOT NULL,  -- exclusive; turnaround buffer applied at query time
    BlockType           TINYINT           NOT NULL,  -- CalendarBlockType: 1 Reservation, 2 Maintenance, 3 PartnerUnavailable
    Status              TINYINT           NOT NULL,  -- ReservationStatus: 1 Active, 2 Expired, 3 Released, 4 Consumed
    SourceBookingId     INT               NULL,
    ExpiresAt           DATETIME2(0)      NULL,      -- payment hold expiry for unpaid reservations
    Notes               NVARCHAR(500)     NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_VehicleCalendarBlock_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           INT               NOT NULL,
    CONSTRAINT FK_VehicleCalendarBlock_PartnerVehicle FOREIGN KEY (PartnerVehicleId) REFERENCES dbo.PartnerVehicle (PartnerVehicleId),
    CONSTRAINT FK_VehicleCalendarBlock_Booking        FOREIGN KEY (SourceBookingId)  REFERENCES dbo.Booking (BookingId),
    CONSTRAINT FK_VehicleCalendarBlock_CreatedBy      FOREIGN KEY (CreatedBy)        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_VehicleCalendarBlock_Interval CHECK (EndAt > StartAt),
    CONSTRAINT CK_VehicleCalendarBlock_Type     CHECK (BlockType IN (1,2,3)),
    CONSTRAINT CK_VehicleCalendarBlock_Status   CHECK (Status IN (1,2,3,4)),
    CONSTRAINT CK_VehicleCalendarBlock_Source   CHECK ((BlockType = 1 AND SourceBookingId IS NOT NULL) OR (BlockType <> 1))
);
-- Overlap check: ExistingStartAt < RequestedEndAt AND ExistingEndAt > RequestedStartAt, under sp_getapplock('vehicle:{id}').
CREATE INDEX IX_VehicleCalendarBlock_Overlap ON dbo.VehicleCalendarBlock (PartnerVehicleId, StartAt, EndAt) WHERE Status IN (1,4);
CREATE INDEX IX_VehicleCalendarBlock_Expiry  ON dbo.VehicleCalendarBlock (ExpiresAt) WHERE Status = 1 AND ExpiresAt IS NOT NULL;
CREATE UNIQUE INDEX UX_VehicleCalendarBlock_BookingActive ON dbo.VehicleCalendarBlock (SourceBookingId) WHERE SourceBookingId IS NOT NULL AND Status IN (1,4);

/* ---------------------------------------------------------------- Payments */

CREATE TABLE dbo.Payment
(
    PaymentId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
    BookingId           INT               NOT NULL,
    Gateway             TINYINT           NOT NULL CONSTRAINT DF_Payment_Gateway DEFAULT (1), -- PaymentGateway: 1 Razorpay
    GatewayOrderId      VARCHAR(64)       NOT NULL,
    GatewayPaymentId    VARCHAR(64)       NULL,
    Amount              DECIMAL(12,2)     NOT NULL,
    Currency            CHAR(3)           NOT NULL CONSTRAINT DF_Payment_Currency DEFAULT ('INR'),
    PaymentStatus       TINYINT           NOT NULL,  -- PaymentStatus: 1 Pending, 2 Authorized, 3 Paid, 4 Failed, 5 RefundPending, 6 Refunded, 7 PartiallyRefunded
    IdempotencyKey      VARCHAR(64)       NOT NULL,
    PaidAt              DATETIME2(0)      NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Payment_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Payment_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    RowVersion          ROWVERSION        NOT NULL,
    CONSTRAINT UQ_Payment_GatewayOrder UNIQUE (Gateway, GatewayOrderId),
    CONSTRAINT UQ_Payment_Idempotency  UNIQUE (IdempotencyKey),
    CONSTRAINT FK_Payment_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Booking (BookingId),
    CONSTRAINT CK_Payment_Gateway  CHECK (Gateway IN (1)),
    CONSTRAINT CK_Payment_Status   CHECK (PaymentStatus IN (1,2,3,4,5,6,7)),
    CONSTRAINT CK_Payment_Amount   CHECK (Amount > 0),
    CONSTRAINT CK_Payment_Currency CHECK (Currency = 'INR')
);
CREATE UNIQUE INDEX UX_Payment_GatewayPayment ON dbo.Payment (Gateway, GatewayPaymentId) WHERE GatewayPaymentId IS NOT NULL;
CREATE INDEX IX_Payment_Booking ON dbo.Payment (BookingId);
CREATE INDEX IX_Payment_Status  ON dbo.Payment (PaymentStatus, CreatedAt);

CREATE TABLE dbo.PaymentWebhookEvent
(
    WebhookEventId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentWebhookEvent PRIMARY KEY,
    Gateway             TINYINT              NOT NULL,
    GatewayEventId      VARCHAR(100)         NOT NULL,  -- x-razorpay-event-id
    EventType           VARCHAR(100)         NOT NULL,
    Payload             NVARCHAR(MAX)        NOT NULL,
    ProcessingStatus    TINYINT              NOT NULL,  -- WebhookProcessingStatus: 1 Received, 2 Processed, 3 Failed, 4 Ignored
    ReceivedAt          DATETIME2(0)         NOT NULL CONSTRAINT DF_PaymentWebhookEvent_ReceivedAt DEFAULT (SYSUTCDATETIME()),
    ProcessedAt         DATETIME2(0)         NULL,
    FailureReason       NVARCHAR(1000)       NULL,
    CONSTRAINT UQ_PaymentWebhookEvent UNIQUE (Gateway, GatewayEventId),
    CONSTRAINT CK_PaymentWebhookEvent_Status CHECK (ProcessingStatus IN (1,2,3,4))
);

CREATE TABLE dbo.Refund
(
    RefundId            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Refund PRIMARY KEY,
    PaymentId           INT               NOT NULL,
    GatewayRefundId     VARCHAR(64)       NULL,
    Amount              DECIMAL(12,2)     NOT NULL,
    RefundStatus        TINYINT           NOT NULL,  -- RefundStatus: 1 Requested, 2 Processing, 3 Processed, 4 Failed
    Reason              NVARCHAR(500)     NULL,
    IdempotencyKey      VARCHAR(64)       NOT NULL,
    RequestedByUserId   INT               NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Refund_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Refund_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Refund_Idempotency UNIQUE (IdempotencyKey),
    CONSTRAINT FK_Refund_Payment     FOREIGN KEY (PaymentId)         REFERENCES dbo.Payment (PaymentId),
    CONSTRAINT FK_Refund_RequestedBy FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Refund_Status CHECK (RefundStatus IN (1,2,3,4)),
    CONSTRAINT CK_Refund_Amount CHECK (Amount > 0)
);
CREATE UNIQUE INDEX UX_Refund_GatewayRefund ON dbo.Refund (GatewayRefundId) WHERE GatewayRefundId IS NOT NULL;

CREATE TABLE dbo.PartnerSettlement
(
    SettlementId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartnerSettlement PRIMARY KEY,
    BookingId           INT               NOT NULL,
    RentalPartnerId     INT               NOT NULL,
    GrossAmount         DECIMAL(12,2)     NOT NULL,
    CommissionAmount    DECIMAL(12,2)     NOT NULL,
    DeliveryCost        DECIMAL(12,2)     NOT NULL CONSTRAINT DF_PartnerSettlement_Delivery DEFAULT (0),
    OtherDeductions     DECIMAL(12,2)     NOT NULL CONSTRAINT DF_PartnerSettlement_Other DEFAULT (0),
    NetPayout           DECIMAL(12,2)     NOT NULL,
    SettlementStatus    TINYINT           NOT NULL,  -- PartnerSettlementStatus: 1 Pending, 2 Approved, 3 Paid, 4 OnHold, 5 Cancelled
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_PartnerSettlement_CreatedAt DEFAULT (SYSUTCDATETIME()),
    PaidAt              DATETIME2(0)      NULL,
    CONSTRAINT UQ_PartnerSettlement_Booking UNIQUE (BookingId),
    CONSTRAINT FK_PartnerSettlement_Booking       FOREIGN KEY (BookingId)       REFERENCES dbo.Booking (BookingId),
    CONSTRAINT FK_PartnerSettlement_RentalPartner FOREIGN KEY (RentalPartnerId) REFERENCES dbo.RentalPartner (RentalPartnerId),
    CONSTRAINT CK_PartnerSettlement_Status CHECK (SettlementStatus IN (1,2,3,4,5))
    -- NetPayout formula is enforced in SettlementService once the payout policy is confirmed (see docs/PLAN.md, open questions).
);
CREATE INDEX IX_PartnerSettlement_Partner ON dbo.PartnerSettlement (RentalPartnerId, SettlementStatus);

/* ---------------------------------------------------------------- Notifications */

CREATE TABLE dbo.NotificationTemplate
(
    TemplateId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationTemplate PRIMARY KEY,
    EventType       SMALLINT          NOT NULL,  -- NotificationEventType
    Channel         TINYINT           NOT NULL,  -- NotificationChannel
    Language        VARCHAR(10)       NOT NULL CONSTRAINT DF_NotificationTemplate_Language DEFAULT ('en'),
    Subject         NVARCHAR(200)     NULL,
    Body            NVARCHAR(MAX)     NOT NULL,
    ProviderTemplateName VARCHAR(100) NULL,      -- WhatsApp approved template name
    IsActive        BIT               NOT NULL CONSTRAINT DF_NotificationTemplate_IsActive DEFAULT (1),
    CONSTRAINT CK_NotificationTemplate_Channel CHECK (Channel IN (1,2,3,4,5))
);
CREATE UNIQUE INDEX UX_NotificationTemplate_Active ON dbo.NotificationTemplate (EventType, Channel, Language) WHERE IsActive = 1;

CREATE TABLE dbo.NotificationOutbox
(
    OutboxId            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationOutbox PRIMARY KEY,
    EventId             UNIQUEIDENTIFIER     NOT NULL,
    EventType           SMALLINT             NOT NULL,  -- NotificationEventType
    Payload             NVARCHAR(MAX)        NOT NULL,  -- JSON, no payment credentials
    ProcessingStatus    TINYINT              NOT NULL CONSTRAINT DF_NotificationOutbox_Status DEFAULT (1), -- OutboxStatus: 1 Pending, 2 Processing, 3 Processed, 4 Failed
    RetryCount          INT                  NOT NULL CONSTRAINT DF_NotificationOutbox_Retry DEFAULT (0),
    NextRetryAt         DATETIME2(0)         NULL,
    LockedUntil         DATETIME2(0)         NULL,      -- worker lease
    CreatedAt           DATETIME2(0)         NOT NULL CONSTRAINT DF_NotificationOutbox_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ProcessedAt         DATETIME2(0)         NULL,
    CONSTRAINT UQ_NotificationOutbox_EventId UNIQUE (EventId),
    CONSTRAINT CK_NotificationOutbox_Status CHECK (ProcessingStatus IN (1,2,3,4))
);
CREATE INDEX IX_NotificationOutbox_Due ON dbo.NotificationOutbox (ProcessingStatus, NextRetryAt) WHERE ProcessingStatus IN (1,2);

CREATE TABLE dbo.Notification
(
    NotificationId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notification PRIMARY KEY,
    RecipientUserId     INT                  NOT NULL,
    BookingId           INT                  NULL,
    OutboxEventId       UNIQUEIDENTIFIER     NULL,
    Channel             TINYINT              NOT NULL,
    EventType           SMALLINT             NOT NULL,
    TemplateId          INT                  NULL,
    RecipientAddress    NVARCHAR(256)        NULL,  -- email / phone; NULL for in-app
    Title               NVARCHAR(200)        NULL,  -- rendered, for in-app display
    Body                NVARCHAR(2000)       NULL,
    NotificationStatus  TINYINT              NOT NULL,  -- NotificationStatus: 1 Pending, 2 Processing, 3 Sent, 4 Delivered, 5 Failed, 6 Cancelled
    IsRead              BIT                  NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT (0),
    ProviderMessageId   VARCHAR(200)         NULL,
    IdempotencyKey      VARCHAR(150)         NOT NULL,  -- {EventId}:{Channel}:{RecipientUserId}
    CreatedAt           DATETIME2(0)         NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT (SYSUTCDATETIME()),
    SentAt              DATETIME2(0)         NULL,
    FailureReason       NVARCHAR(1000)       NULL,
    CONSTRAINT UQ_Notification_Idempotency UNIQUE (IdempotencyKey),
    CONSTRAINT FK_Notification_Recipient FOREIGN KEY (RecipientUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Notification_Booking   FOREIGN KEY (BookingId)       REFERENCES dbo.Booking (BookingId),
    CONSTRAINT FK_Notification_Template  FOREIGN KEY (TemplateId)      REFERENCES dbo.NotificationTemplate (TemplateId),
    CONSTRAINT CK_Notification_Channel CHECK (Channel IN (1,2,3,4,5)),
    CONSTRAINT CK_Notification_Status  CHECK (NotificationStatus IN (1,2,3,4,5,6))
);
CREATE INDEX IX_Notification_Recipient ON dbo.Notification (RecipientUserId, CreatedAt DESC);
CREATE INDEX IX_Notification_Failed    ON dbo.Notification (NotificationStatus, CreatedAt) WHERE NotificationStatus = 5;

CREATE TABLE dbo.NotificationAttempt
(
    NotificationAttemptId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationAttempt PRIMARY KEY,
    NotificationId        BIGINT               NOT NULL,
    AttemptNumber         SMALLINT             NOT NULL,
    AttemptedAt           DATETIME2(0)         NOT NULL CONSTRAINT DF_NotificationAttempt_AttemptedAt DEFAULT (SYSUTCDATETIME()),
    Result                TINYINT              NOT NULL,  -- NotificationAttemptResult: 1 Success, 2 TransientFailure, 3 PermanentFailure
    ProviderResponse      NVARCHAR(2000)       NULL,
    ErrorMessage          NVARCHAR(1000)       NULL,
    CONSTRAINT UQ_NotificationAttempt UNIQUE (NotificationId, AttemptNumber),
    CONSTRAINT FK_NotificationAttempt_Notification FOREIGN KEY (NotificationId) REFERENCES dbo.Notification (NotificationId),
    CONSTRAINT CK_NotificationAttempt_Result CHECK (Result IN (1,2,3))
);

/* ---------------------------------------------------------------- Audit */

CREATE TABLE dbo.AuditLog
(
    AuditLogId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    ActorUserId     INT                  NULL,     -- NULL = system
    Action          VARCHAR(100)         NOT NULL,
    EntityType      VARCHAR(100)         NOT NULL,
    EntityId        VARCHAR(50)          NOT NULL,
    Timestamp       DATETIME2(0)         NOT NULL CONSTRAINT DF_AuditLog_Timestamp DEFAULT (SYSUTCDATETIME()),
    CorrelationId   VARCHAR(64)          NULL,
    Details         NVARCHAR(MAX)        NULL,     -- JSON, never secrets
    CONSTRAINT FK_AuditLog_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users (UserId)
);
CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog (EntityType, EntityId, Timestamp DESC);
CREATE INDEX IX_AuditLog_Time   ON dbo.AuditLog (Timestamp DESC);

COMMIT TRANSACTION;
