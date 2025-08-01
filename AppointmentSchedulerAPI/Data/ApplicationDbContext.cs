using AppointmentSchedulerAPI.Models;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<Appointment> Appointments { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the precision for the Price property in the Service entity
            modelBuilder.Entity<Service>().Property(s => s.Price).HasPrecision(18, 2);
        }
    }
}