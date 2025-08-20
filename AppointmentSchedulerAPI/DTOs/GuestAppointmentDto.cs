namespace AppointmentSchedulerAPI.DTOs
{
    public class GuestAppointmentDto
    {
        public int ServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public string GuestName { get; set; }
        public string GuestPhone { get; set; }
    }
}
