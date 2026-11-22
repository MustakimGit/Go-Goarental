namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in BookingAssignment.AssignmentStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum BookingAssignmentStatus : byte
{
    Candidate = 1,
    Reserved = 2,
    Failed = 3,
    Released = 4,
    Reassigned = 5,
}
