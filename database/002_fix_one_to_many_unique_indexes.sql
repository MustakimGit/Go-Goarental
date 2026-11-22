/*
    002 - Keep Booking -> BookingAssignment and Booking -> VehicleCalendarBlock as one-to-many in EF scaffolding.

    EF Core's scaffolder treats ANY unique index whose columns equal a foreign key (even a filtered one)
    as a one-to-one relationship. The filtered indexes from 001 were keyed only on BookingId, so EF generated
    Booking.BookingAssignment / Booking.VehicleCalendarBlock as single navigations.

    The replacements add a second key column that is constant within each index's filter, so the
    business guarantee is unchanged ("one live reservation per booking") while the index no longer
    matches the FK column set exactly.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP INDEX UX_BookingAssignment_OneReserved ON dbo.BookingAssignment;
CREATE UNIQUE INDEX UX_BookingAssignment_OneReserved
    ON dbo.BookingAssignment (BookingId, AssignmentStatus)
    WHERE AssignmentStatus = 2;

-- BlockType is always 1 (Reservation) when SourceBookingId is set (CK_VehicleCalendarBlock_Source).
DROP INDEX UX_VehicleCalendarBlock_BookingActive ON dbo.VehicleCalendarBlock;
CREATE UNIQUE INDEX UX_VehicleCalendarBlock_BookingActive
    ON dbo.VehicleCalendarBlock (SourceBookingId, BlockType)
    WHERE SourceBookingId IS NOT NULL AND Status IN (1,4);

COMMIT TRANSACTION;
