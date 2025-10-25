namespace AppointmentSchedulerAPI.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int? ClientId { get; set; } // Nullable, for registered users
        public int ServiceId { get; set; }
        public Service Service { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }

        // New fields for guests
        public string? GuestName { get; set; }
        public string? GuestPhone { get; set; }
    }
}