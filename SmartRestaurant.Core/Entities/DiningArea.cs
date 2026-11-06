using System.Collections.Generic;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Dining floor area or section (e.g. Main Hall, VIP Room, Outdoor Terrace).
/// </summary>
public class DiningArea
{
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public int FloorNumber { get; set; } = 1;

    public virtual ICollection<DiningTable> Tables { get; set; } = new List<DiningTable>();
}
