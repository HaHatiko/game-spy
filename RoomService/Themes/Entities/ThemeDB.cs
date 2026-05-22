namespace RoomService
{
    public class ThemeDB
    {
        public int Id { get; set; }
        public string Theme { get; set; } = string.Empty;
        public List<WordDB> Words { get; set; } = new();
    }
}