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
            // ה-ClaimsPrincipal 'User' זמין לנו אוטומטית בכל קונטרולר מאובטח בזכות ה-[Authorize].
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                // מצב כזה לא אמור לקרות אם [Authorize] עובד, אבל זו בדיקת בטיחות טובה.
                return Unauthorized();
            }

            // 2. ולידציות
            // 2a. ודאי שהשירות שהלקוח ביקש קיים בבסיס הנתונים
            var service = await _context.Services.FindAsync(request.ServiceId);
            if (service == null)
            {
                return BadRequest(new { message = "Service not found." });
            }

            // ============================ התיקון הקריטי מתחיל כאן ============================
            // 2b. ודאי שהמשבצת המבוקשת באמת פנויה לפני יצירת התור.
            // זוהי בדיקה חיונית למניעת מצב שבו שני משתמשים מזמינים את אותו תור באותו זמן (Race Condition).
            var isSlotTaken = await _context.Appointments.AnyAsync(a => a.StartTime == request.StartTime);
            if (isSlotTaken)
            {
                // אנחנו מחזירים סטטוס 409 Conflict, שזו התשובה הסמנטית הנכונה ביותר
                // למצב שבו הבקשה תקינה אבל מתנגשת עם המצב הנוכחי של המערכת.
                return Conflict(new { message = "This time slot has just been taken. Please choose another one." });
            }
            // ===============================================================================

            // 3. יצירת אובייקט התור החדש
            var newAppointment = new AppointmentSchedulerAPI.Models.Appointment
            {
                ServiceId = request.ServiceId,
                ClientId = userId, // <-- כאן אנחנו משייכים את התור למשתמש הרשום ששלףנו מהטוקן
                StartTime = request.StartTime,
                EndTime = request.StartTime.AddMinutes(service.DurationInMinutes),
                Status = "Confirmed",
                GuestName = null, // זה לא תור של אורח
                GuestPhone = null // זה לא תור של אורח
            };

            // 4. שמירה בבסיס הנתונים
            _context.Appointments.Add(newAppointment);
            await _context.SaveChangesAsync();

            // 5. החזרת תשובה מוצלחת
            // מחזירים סטטוס 201 Created, שהוא הסטנדרט לפעולת יצירה מוצלחת,
            // יחד עם התור החדש שנוצר.
            return CreatedAtAction(nameof(BookAppointment), new { id = newAppointment.Id }, newAppointment);
        }
    }
}