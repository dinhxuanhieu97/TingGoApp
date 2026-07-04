namespace TingGo.Modules.Venues.Domain;

/// <summary>Giờ mở cửa theo ngày (CUS-09). day_of_week: ISO 8601 — 1=Thứ Hai ... 7=Chủ Nhật.</summary>
public sealed class OpeningHour
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public int DayOfWeekIso { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsClosed { get; set; }
}
