namespace AppointmentSchedulerAPI.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int ClientId { get; set; } // Foreign Key to User table
        public int ServiceId { get; set; } // Foreign Key to Service table
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } // e.g., "Confirmed", "Cancelled"
    }
}