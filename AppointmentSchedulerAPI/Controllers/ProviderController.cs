using AppointmentSchedulerAPI.Data;
using AppointmentSchedulerAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSchedulerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProviderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProviderController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("services")] // Defines the route: GET /api/provider/services
        public async Task<IActionResult> GetServices()
        {
            var services = await _context.Services.ToListAsync();
            return Ok(services);
        }
        [HttpGet("availability")] // Defines the route: GET /api/provider/availability
        public async Task<IActionResult> GetAvailability([FromQuery] int serviceId)
        {
            // 1. קבל את פרטי השירות המבוקש כדי לדעת את אורך התור
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null)
            {
                return BadRequest("Service not found.");
            }
            var slotDurationInMinutes = service.DurationInMinutes; // e.g., 20 minutes

            // 2. הגדר את טווח הזמן לבדיקה
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(14); // Let's check for 2 weeks

            // 3. שלוף את כל המידע הרלוונטי מבסיס הנתונים בפעם אחת
            var workHours = await _context.Availabilities.ToListAsync();
            var existingAppointments = await _context.Appointments
                .Where(a => a.StartTime >= startDate && a.StartTime < endDate)
                .ToListAsync();
            var timeBlocks = await _context.TimeBlocks
                .Where(tb => tb.StartTime < endDate && tb.EndTime > startDate)
                .ToListAsync();

            var availableSlots = new List<DateTime>();

            // 4. עבור על כל יום בטווח
            for (var day = startDate; day < endDate; day = day.AddDays(1))
            {
                // 5. קבע את שעות העבודה הבסיסיות לאותו יום
                var dayWorkHours = workHours.FirstOrDefault(wh => wh.DayOfWeek == day.DayOfWeek);
                if (dayWorkHours == null) continue; // לא עובד ביום הזה

                DateTime potentialSlot = day.Date + dayWorkHours.StartTime;
                DateTime endOfDay = day.Date + dayWorkHours.EndTime;

                // 6. עבור על כל משבצת זמן אפשרית באותו יום
                while (potentialSlot.AddMinutes(slotDurationInMinutes) <= endOfDay)
                {
                    DateTime slotEnd = potentialSlot.AddMinutes(slotDurationInMinutes);

                    // 7. בדוק חוקים - האם המשבצת הזו תקינה?
                    bool isBooked = existingAppointments.Any(a => a.StartTime == potentialSlot);
                    bool isBlocked = timeBlocks.Any(tb => !tb.IsAvailable && potentialSlot < tb.EndTime && slotEnd > tb.StartTime);

                    if (!isBooked && !isBlocked)
                    {
                        // הוסף את המשבצת אם היא פנויה ולא חסומה
                        availableSlots.Add(potentialSlot);
                    }

                    // קפוץ למשבצת הבאה (פה נכנסת הגמישות!)
                    potentialSlot = potentialSlot.AddMinutes(slotDurationInMinutes);
                }
            }

            // 8. הוסף זמינויות מיוחדות (אם יש)
            var specialAvailabilities = timeBlocks.Where(tb => tb.IsAvailable).ToList();
            foreach (var specialBlock in specialAvailabilities)
            {
                DateTime potentialSlot = specialBlock.StartTime;
                while (potentialSlot.AddMinutes(slotDurationInMinutes) <= specialBlock.EndTime)
                {
                    if (!availableSlots.Contains(potentialSlot))
                    {
                        availableSlots.Add(potentialSlot);
                    }
                    potentialSlot = potentialSlot.AddMinutes(slotDurationInMinutes);
                }
            }

            return Ok(availableSlots.OrderBy(s => s));
        }
        [HttpPost("book-as-guest")] // POST /api/provider/book-as-guest
        public async Task<IActionResult> BookAsGuest([FromBody] GuestAppointmentDto request)
        {
            // Basic validation (can be improved)
            var service = await _context.Services.FindAsync(request.ServiceId);
            if (service == null)
            {
                return BadRequest("Service not found.");
            }
            var isSlotAvailable = !(await _context.Appointments.AnyAsync(a => a.StartTime == request.StartTime));
            if (!isSlotAvailable)
            {
                return BadRequest("This time slot is no longer available.");
            }
            var newAppointment = new AppointmentSchedulerAPI.Models.Appointment
            {
                ServiceId = request.ServiceId,
                StartTime = request.StartTime,
                EndTime = request.StartTime.AddMinutes(service.DurationInMinutes),
                Status = "Confirmed",
                GuestName = request.GuestName,
                GuestPhone = request.GuestPhone,
                ClientId = null // Explicitly null for a guest
            };
            _context.Appointments.Add(newAppointment);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Appointment booked successfully for guest." });
        }
    }
}