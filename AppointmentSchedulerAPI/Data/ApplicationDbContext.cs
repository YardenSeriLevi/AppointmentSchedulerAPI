using AppointmentSchedulerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppointmentSchedulerAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Availability> Availabilities { get; set; }
        public DbSet<TimeBlock> TimeBlocks { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // הגדרת דיוק המחיר
            modelBuilder.Entity<Service>().Property(s => s.Price).HasPrecision(18, 2);

            // ============================ התיקון הקריטי מתחיל כאן ============================

            // יצירת ValueConverter לטיפול בתאריכים שאינם Nullable (כמו StartTime)
            var utcConverter = new UtcValueConverter();

            // עובר על כל ישות במודל שלך (User, Service, Appointment, וכו')
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // עובר על כל מאפיין בתוך הישות
                foreach (var property in entityType.GetProperties())
                {
                    // אם המאפיין הוא DateTime ואינו Nullable
                    if (property.ClrType == typeof(DateTime))
                    {
                        // הגדרת ה-Converter שיפעיל SpecifyKind(..., Utc) בשליפה
                        property.SetValueConverter(utcConverter);
                    }

                    // טיפול ב-DateTime? (אם יש לך כאלו)
                    if (property.ClrType == typeof(DateTime?))
                    {
                        // צריך Converter שונה ל-Nullable
                        property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                            v => v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                    }
                }
            }
        }
    }
}