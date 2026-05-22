using Microsoft.EntityFrameworkCore;

namespace RoomService{
    public class ThemeContext : DbContext
    {
        public DbSet<ThemeDB> Themes {get; set;}
        public DbSet<WordDB> Words {get; set;}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
        }
    }
}