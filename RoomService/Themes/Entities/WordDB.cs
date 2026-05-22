namespace RoomService
{
    public class WordDB
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public int ThemeId { get; set; }
        public ThemeDB? ThemeNavigation { get; set; }
    }
}