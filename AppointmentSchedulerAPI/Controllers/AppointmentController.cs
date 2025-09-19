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
        [HttpPost] // POST /api/appointment
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentDto request)
        {
            // 1. חילוץ מזהה המשתמש מהטוקן
            // ה-ClaimsPrincipal 'User' זמין לנו אוטומטית בכל קונטרולר מאובטח.
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Unauthorized(); // אם הטוקן לא תקין או שאין בו ID
            }

            // 2. ולידציות
            // 2a. ודאי שהשירות קיים
            var service = await _context.Services.FindAsync(request.ServiceId);
            if (service == null)
            {
                return BadRequest(new { message = "Service not found." });
            }

            // 2b. ודא שהמשבצת המבוקשת באמת פנויה (החלק הכי קריטי)
            // (זוהי בדיקה חוזרת בצד השרת, למקרה שמישהו אחר תפס את התור בשנייה האחרונה)
            var isSlotTaken = await _context.Appointments.AnyAsync(a => a.StartTime == request.StartTime);
            if (isSlotTaken)
            {
                return BadRequest(new { message = "This time slot has just been taken. Please choose another one." });
            }

            // כאן אפשר להוסיף ולידציה מורכבת יותר שבודקת מול מנוע הזמינות,
            // אבל לצורך הפרויקט, בדיקה מול תורים קיימים היא מספקת.

            // 3. יצירת התור החדש
            var newAppointment = new AppointmentSchedulerAPI.Models.Appointment
            {
                ServiceId = request.ServiceId,
                ClientId = userId, // <-- שיוך למשתמש הרשום
                StartTime = request.StartTime,
                EndTime = request.StartTime.AddMinutes(service.DurationInMinutes),
                Status = "Confirmed",
                GuestName = null, // לא אורח
                GuestPhone = null // לא אורח
            };

            // 4. שמירה בבסיס הנתונים
            _context.Appointments.Add(newAppointment);
            await _context.SaveChangesAsync();

            // 5. החזרת תשובה מוצלחת
            // נהוג להחזיר את האובייקט החדש שנוצר יחד עם סטטוס 201 Created
            return CreatedAtAction(nameof(BookAppointment), new { id = newAppointment.Id }, newAppointment);
        }
    }
}