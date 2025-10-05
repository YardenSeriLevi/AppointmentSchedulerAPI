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
        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] int serviceId)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return BadRequest("Service not found.");
            var slotDurationInMinutes = service.DurationInMinutes;

            var israelTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
            var nowInIsrael = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, israelTimeZone);
            var utcOffset = israelTimeZone.GetUtcOffset(nowInIsrael);

            var scanStartUtc = DateTime.UtcNow; // נקודת התחלה היא תמיד UTC
            var scanEndDate = scanStartUtc.Date.AddDays(14);

            var workHours = await _context.Availabilities.ToListAsync();

            var appointmentsListUtc = await _context.Appointments
                .Where(a => a.StartTime >= scanStartUtc && a.StartTime < scanEndDate)
                .Select(a => a.StartTime)
                .ToListAsync();
            var existingAppointmentsUtc = new HashSet<DateTime>(appointmentsListUtc);

            var timeBlocksUtc = await _context.TimeBlocks
                .Where(tb => tb.StartTime < scanEndDate && tb.EndTime > scanStartUtc)
                .ToListAsync();

            var availableSlotsUtc = new HashSet<DateTime>();

            for (var day = scanStartUtc.Date; day < scanEndDate; day = day.AddDays(1))
            {
                var dayWorkHours = workHours.FirstOrDefault(wh => wh.DayOfWeek == day.DayOfWeek);
                if (dayWorkHours == null) continue;

                // 1. צור את שעת ההתחלה והסיום כתאריך מקומי
                var workStartLocal = day.Date + dayWorkHours.StartTime;
                var workEndLocal = day.Date + dayWorkHours.EndTime;

                // 2. המר אותם ל-UTC על ידי הפחתה ידנית של הפרש השעות
                var workStartUtc = workStartLocal - utcOffset;
                var workEndUtc = workEndLocal - utcOffset;

                var potentialSlotUtc = workStartUtc;
                if (potentialSlotUtc < scanStartUtc)
                {
                    potentialSlotUtc = scanStartUtc;
                }

                while (potentialSlotUtc.AddMinutes(slotDurationInMinutes) <= workEndUtc)
                {
                    var slotEndUtc = potentialSlotUtc.AddMinutes(slotDurationInMinutes);

                    if (!existingAppointmentsUtc.Contains(potentialSlotUtc) &&
                        !timeBlocksUtc.Any(tb => !tb.IsAvailable && potentialSlotUtc < tb.EndTime && slotEndUtc > tb.StartTime))
                    {
                        availableSlotsUtc.Add(potentialSlotUtc);
                    }

                    potentialSlotUtc = potentialSlotUtc.AddMinutes(slotDurationInMinutes);
                }
            }

            // ============================ הוספנו בחזרה את החלק החסר ============================
            // הטיפול בזמינויות מיוחדות (כבר ב-UTC מה-DB)
            var specialAvailabilities = timeBlocksUtc.Where(tb => tb.IsAvailable).ToList();
            foreach (var specialBlock in specialAvailabilities)
            {
                var potentialSlotUtc = specialBlock.StartTime;
                if (potentialSlotUtc < scanStartUtc)
                {
                    potentialSlotUtc = scanStartUtc;
                }

                while (potentialSlotUtc.AddMinutes(slotDurationInMinutes) <= specialBlock.EndTime)
                {
                    if (!existingAppointmentsUtc.Contains(potentialSlotUtc))
                    {
                        availableSlotsUtc.Add(potentialSlotUtc);
                    }
                    potentialSlotUtc = potentialSlotUtc.AddMinutes(slotDurationInMinutes);
                }
            }
            // =================================================================================

            return Ok(availableSlotsUtc.OrderBy(s => s));
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
                return BadRequest("מצטערים, התור שביקשת אינו זמין יותר, אנה בחר בתור אחר ");
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