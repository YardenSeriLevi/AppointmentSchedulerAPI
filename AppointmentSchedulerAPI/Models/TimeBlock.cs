namespace AppointmentSchedulerAPI.Models
{
    public class TimeBlock
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; } // <-- הוספנו את השורה הזו
        public bool IsAvailable { get; set; }
    }

}