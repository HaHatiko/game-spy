using authorization;

namespace RoomService
{
    public interface IRoomService
    {
        void CreateRoom(RoomType type, User creator);
        Room? GetRoomByUserId(UserId id);
        Room? GetRoomByRoomId(Guid id);
        IReadOnlyDictionary<Guid, Room> GetRooms();
    }
}