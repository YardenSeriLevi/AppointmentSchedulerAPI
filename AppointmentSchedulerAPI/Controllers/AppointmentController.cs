using AppointmentSchedulerAPI.Data;
using Microsoft.AspNetCore.Authorization; // <-- חשוב!
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Security.Claims;
using AppointmentSchedulerAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSchedulerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // <-- כל הפעולות כאן דורשות משתמש מחובר!
    public class AppointmentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppointmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        //נקודת קצה עבור הזמנת תור למשתמש מחובר למערכת
        [HttpPost] // מגדיר את הנתיב כ- POST /api/appointment
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentDto request)
        {
            // 1. חילוץ מזהה המשתמש מהטוקן
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            // --- התיקון מתחיל כאן ---
            // 1b. שלוף את כל פרטי המשתמש מבסיס הנתונים
            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null)
            {
                return Unauthorized("User not found."); // מצב אבטחה חריג
            }
            // --- סוף התיקון ---

            // 2. ולידציות
            var service = await _context.Services.FindAsync(request.ServiceId);
            if (service == null)
            {
                return BadRequest(new { message = "Service not found." });
            }

            var isSlotTaken = await _context.Appointments.AnyAsync(a => a.StartTime == request.StartTime);
            if (isSlotTaken)
            {
                return Conflict(new { message = "This time slot has just been taken." });
            }

            // 3. יצירת התור החדש
            var newAppointment = new AppointmentSchedulerAPI.Models.Appointment
            {
                ServiceId = request.ServiceId,
                ClientId = userId,
                StartTime = request.StartTime,
                EndTime = request.StartTime.AddMinutes(service.DurationInMinutes),
                Status = "Confirmed",
                GuestName = currentUser.FullName, // <-- בונוס: נשמור גם את השם
                GuestPhone = currentUser.PhoneNumber // <-- כאן אנחנו מעבירים את הטלפון!
            };

            // 4. שמירה בבסיס הנתונים
            _context.Appointments.Add(newAppointment);
            await _context.SaveChangesAsync();

            // 5. החזרת תשובה מוצלחת
            return CreatedAtAction(nameof(BookAppointment), new { id = newAppointment.Id }, newAppointment);
        }

        [HttpGet("my-appointments")] // Defines the route: GET /api/appointment/my-appointments
        public async Task<IActionResult> GetMyAppointments()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var now = DateTime.UtcNow;

            // בניית השאילתה הבסיסית
            var baseQuery = _context.Appointments
                .Where(a => a.ClientId == userId)
                .Include(a => a.Service); // <-- שלב 1: בקש מ-EF לכלול את פרטי השירות

            // שאילתה לתורים מהעבר
            var pastAppointments = await baseQuery
                .Where(a => a.StartTime < now)
                .OrderByDescending(a => a.StartTime)
                .Take(2)
                .ToListAsync();

            // שאילתה לתורים עתידיים
            var futureAppointments = await baseQuery
                .Where(a => a.StartTime >= now)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            // איחוד התוצאות (עדיין אובייקטים מלאים של Appointment)
            var allAppointments = futureAppointments.Concat(pastAppointments)
                                                      .OrderBy(a => a.StartTime);

            // ============================ שלב 2: עיצוב הנתונים ל-DTO ============================
            var appointmentsDto = allAppointments.Select(a => new AppointmentDetailsDto
            {
                Id = a.Id,
                StartTime = a.StartTime,
                Service = new ServiceDetailsDto
                {
                    Name = a.Service.Name,
                    Price = a.Service.Price,
                    DurationInMinutes = a.Service.DurationInMinutes
                }
            }).ToList();
            // ====================================================================================

            return Ok(appointmentsDto);
        }
    }
}