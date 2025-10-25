namespace AppointmentSchedulerAPI.DTOs
{
    public class AppointmentDetailsDto
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public ServiceDetailsDto Service { get; set; }
    }
}